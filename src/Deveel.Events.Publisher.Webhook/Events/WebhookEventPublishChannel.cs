//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Extensions.Http;

using System.Net.Http.Headers;

namespace Deveel.Events
{
    /// <summary>
    /// An <see cref="IEventPublishChannel{TOptions}"/> and <see cref="IBatchEventPublishChannel{TOptions}"/>
    /// that delivers <see cref="CloudEvent"/> instances (individually or in batches)
    /// to a remote endpoint via HTTP POST, following webhook best practices.
    /// </summary>
    public class WebhookEventPublishChannel : IBatchEventPublishChannel<WebhookPublishOptions>
    {
        private readonly WebhookEventPublishChannelOptions _defaults;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;

        private readonly IDictionary<WebhookSignatureAlgorithm, IWebhookSignatureProvider> _signatureProviders;
        private readonly IDictionary<string, IEventSerializer> _serializers;

        /// <summary>Initialises the channel.</summary>
        public WebhookEventPublishChannel(
            IOptions<WebhookEventPublishChannelOptions> options,
            IHttpClientFactory httpClientFactory,
            IEnumerable<IWebhookSignatureProvider>? signatureProviders = null,
            IEnumerable<IEventSerializer>? serializers = null,
            ILogger<WebhookEventPublishChannel>? logger = null)
        {
            _defaults          = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger            = logger ?? NullLogger<WebhookEventPublishChannel>.Instance;

            // Signature providers
            _signatureProviders = new Dictionary<WebhookSignatureAlgorithm, IWebhookSignatureProvider>
            {
                [WebhookSignatureAlgorithm.HmacSha256] = HmacSha256SignatureProvider.Default,
                [WebhookSignatureAlgorithm.HmacSha384] = HmacSha384SignatureProvider.Default,
                [WebhookSignatureAlgorithm.HmacSha512] = HmacSha512SignatureProvider.Default,
#pragma warning disable CS0618
                [WebhookSignatureAlgorithm.HmacSha1]   = HmacSha1SignatureProvider.Default,
#pragma warning restore CS0618
            };
            if (signatureProviders != null)
                foreach (var p in signatureProviders)
                    _signatureProviders[p.Algorithm] = p;

            // Message serializers — keyed by format string
            _serializers = new Dictionary<string, IEventSerializer>(StringComparer.OrdinalIgnoreCase)
            {
                [WebhookMessageFormat.Json]           = JsonEventSerializer.Default,
                [WebhookMessageFormat.Xml]            = XmlEventSerializer.Default,
                [WebhookMessageFormat.CloudEventsJson] = CloudEventsJsonSerializer.Default,
                [WebhookMessageFormat.CloudEventsXml]  = CloudEventsXmlSerializer.Default,
            };
            if (serializers != null)
                foreach (var s in serializers)
                    _serializers[s.Format] = s;
        }

        // ── IEventPublishChannel / IEventPublishChannel<WebhookPublishOptions> ─

        /// <inheritdoc/>
        public Task PublishAsync(CloudEvent @event, CancellationToken cancellationToken = default)
            => PublishAsync(@event, null, cancellationToken);

        /// <inheritdoc/>
        public Task PublishAsync(
            CloudEvent @event,
            WebhookPublishOptions? options,
            CancellationToken cancellationToken = default)
        {
            var eff         = Effective(options);
            var serializer  = GetSerializer(eff.Format);
            var payload     = serializer.Serialize(@event);
            var contentType = serializer.ContentType;

            // For a single-event delivery the event-type header makes sense.
            return DeliverAsync(payload, contentType, eventType: @event.Type, eventCount: 1, eff, cancellationToken);
        }

        // ── IBatchEventPublishChannel<WebhookPublishOptions> ────────────────

        /// <inheritdoc/>
        public Task PublishBatchAsync(
            IReadOnlyList<CloudEvent> events,
            CancellationToken cancellationToken = default)
            => PublishBatchAsync(events, null, cancellationToken);

        /// <inheritdoc/>
        public Task PublishBatchAsync(
            IReadOnlyList<CloudEvent> events,
            WebhookPublishOptions? options,
            CancellationToken cancellationToken = default)
        {
            if (events == null || events.Count == 0)
                throw new ArgumentException("The events batch must contain at least one event.", nameof(events));

            var eff         = Effective(options);
            var serializer  = GetSerializer(eff.Format);
            var payload     = serializer.SerializeBatch(events);
            var contentType = serializer.BatchContentType;

            // For batch deliveries the event-type header is omitted (mixed types).
            return DeliverAsync(payload, contentType, eventType: null, eventCount: events.Count, eff, cancellationToken);
        }

        // ── Core delivery ───────────────────────────────────────────────────

        private async Task DeliverAsync(
            byte[] payload,
            string contentType,
            string? eventType,
            int eventCount,
            EffectiveOptions eff,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eff.EndpointUrl))
                throw new InvalidOperationException("The webhook EndpointUrl is not configured.");

            var deliveryId = Guid.NewGuid().ToString("N");
            var provider   = GetSignatureProvider(eff.SignatureAlgorithm);
            var timestamp  = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var algorithm  = eff.SignatureAlgorithm.ToString();

            if (eventType != null)
                _logger.LogDeliveringEvent(eventType, deliveryId, eff.Format, algorithm, eff.EndpointUrl);
            else
                _logger.LogDeliveringBatch(deliveryId, eventCount, eff.Format, algorithm, eff.EndpointUrl);

            // Build the Polly retry policy from the effective (possibly per-delivery-overridden) options.
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TaskCanceledException>(ex => !cancellationToken.IsCancellationRequested)
                .Or<OperationCanceledException>(ex => !cancellationToken.IsCancellationRequested)
                .OrResult(r => eff.RetryableStatusCodes.Contains((int)r.StatusCode))
                .WaitAndRetryAsync(
                    eff.MaxRetryCount,
                    attempt => TimeSpan.FromMilliseconds(
                        eff.RetryDelay.TotalMilliseconds * Math.Pow(eff.RetryBackoffMultiplier, attempt - 1)),
                    onRetry: (outcome, delay, attempt, _) =>
                    {
                        if (outcome.Exception != null)
                            _logger.LogRetryOnException(
                                deliveryId, attempt, outcome.Exception.Message, delay.TotalMilliseconds);
                        else
                            _logger.LogRetryOnStatusCode(
                                deliveryId, attempt, (int)outcome.Result.StatusCode, delay.TotalMilliseconds);
                    });

            HttpResponseMessage response;
            try
            {
                response = await retryPolicy.ExecuteAsync(async ct =>
                {
                    // HttpRequestMessage must be recreated per attempt.
                    using var request = BuildRequest(
                        payload, contentType, eventType, deliveryId, timestamp, provider, eff);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(eff.RequestTimeout);
                    return await CreateHttpClient(eff.HttpClientName).SendAsync(request, cts.Token);
                }, cancellationToken);
            }
            catch (Exception ex)
                when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException
                      && !cancellationToken.IsCancellationRequested)
            {
                // Polly exhausted all retry attempts and the last attempt ended in an exception.
                _logger.LogDeliveryFailed(deliveryId, eff.MaxRetryCount + 1);
                throw new WebhookDeliveryException(
                    $"Webhook delivery {deliveryId} failed after {eff.MaxRetryCount + 1} attempt(s).", ex);
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDeliverySucceeded(deliveryId, (int)response.StatusCode);
                return;
            }

            var code = (int)response.StatusCode;

            if (!eff.RetryableStatusCodes.Contains(code))
            {
                // Non-retryable — Polly returned immediately without retrying.
                _logger.LogNonRetryableFailure(deliveryId, code);
                throw new WebhookDeliveryException(
                    $"Webhook delivery {deliveryId} failed with non-retryable status {code}.", code);
            }

            // Retryable status but all retry attempts exhausted — Polly returned the last response.
            _logger.LogDeliveryExhausted(deliveryId, eff.MaxRetryCount + 1);
            throw new WebhookDeliveryException(
                $"Webhook delivery {deliveryId} failed after {eff.MaxRetryCount + 1} attempt(s).", code);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private IEventSerializer GetSerializer(string format)
        {
            if (_serializers.TryGetValue(format, out var s)) return s;
            throw new NotSupportedException(
                $"No serializer registered for webhook message format '{format}'. " +
                $"Register a custom {nameof(IEventSerializer)} via {nameof(EventPublisherBuilderExtensions.UseWebhookMessageSerializer)}.");
        }

        private IWebhookSignatureProvider GetSignatureProvider(WebhookSignatureAlgorithm alg)
        {
            if (_signatureProviders.TryGetValue(alg, out var p)) return p;
            throw new NotSupportedException($"No signature provider registered for algorithm '{alg}'.");
        }

        private HttpClient CreateHttpClient(string? name)
            => _httpClientFactory.CreateClient(
                string.IsNullOrEmpty(name) ? WebhookDefaults.HttpClientName : name);

        private HttpRequestMessage BuildRequest(
            byte[] payload,
            string contentType,
            string? eventType,
            string deliveryId,
            long timestamp,
            IWebhookSignatureProvider provider,
            EffectiveOptions eff)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, eff.EndpointUrl);
            request.Content = new ByteArrayContent(payload);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

            request.Headers.TryAddWithoutValidation(_defaults.DeliveryIdHeaderName, deliveryId);

            if (!string.IsNullOrWhiteSpace(eventType))
                request.Headers.TryAddWithoutValidation(_defaults.EventTypeHeaderName, eventType);

            request.Headers.TryAddWithoutValidation(_defaults.TimestampHeaderName, timestamp.ToString());

            if (!string.IsNullOrWhiteSpace(eff.SigningSecret))
            {
                var sig = provider.ComputeSignature(payload, timestamp, eff.SigningSecret);
                request.Headers.TryAddWithoutValidation(_defaults.SignatureHeaderName, sig);

                if (!string.IsNullOrWhiteSpace(_defaults.SignatureAlgorithmHeaderName))
                    request.Headers.TryAddWithoutValidation(
                        _defaults.SignatureAlgorithmHeaderName, provider.AlgorithmName);
            }

            foreach (var h in eff.AdditionalHeaders)
                request.Headers.TryAddWithoutValidation(h.Key, h.Value);

            return request;
        }

        // ── Effective options resolution ────────────────────────────────────

        private EffectiveOptions Effective(WebhookPublishOptions? options)
        {
            if (options == null)
                return new EffectiveOptions
                {
                    EndpointUrl            = _defaults.EndpointUrl,
                    SigningSecret          = _defaults.SigningSecret,
                    MaxRetryCount          = _defaults.MaxRetryCount,
                    RetryDelay             = _defaults.RetryDelay,
                    RetryBackoffMultiplier = _defaults.RetryBackoffMultiplier,
                    RequestTimeout         = _defaults.RequestTimeout,
                    HttpClientName         = _defaults.HttpClientName,
                    Format                 = _defaults.MessageFormat,
                    SignatureAlgorithm     = _defaults.SignatureAlgorithm,
                    AdditionalHeaders      = _defaults.AdditionalHeaders,
                    RetryableStatusCodes   = _defaults.RetryableStatusCodes,
                };

            var headers = new Dictionary<string, string>(
                _defaults.AdditionalHeaders, StringComparer.OrdinalIgnoreCase);
            if (options.AdditionalHeaders != null)
                foreach (var kv in options.AdditionalHeaders)
                    headers[kv.Key] = kv.Value;

            return new EffectiveOptions
            {
                EndpointUrl            = options.EndpointUrl ?? _defaults.EndpointUrl,
                SigningSecret          = options.SigningSecret ?? _defaults.SigningSecret,
                MaxRetryCount          = options.MaxRetryCount ?? _defaults.MaxRetryCount,
                RetryDelay             = options.RetryDelay ?? _defaults.RetryDelay,
                RetryBackoffMultiplier = options.RetryBackoffMultiplier ?? _defaults.RetryBackoffMultiplier,
                RequestTimeout         = options.RequestTimeout ?? _defaults.RequestTimeout,
                HttpClientName         = _defaults.HttpClientName,
                Format                 = options.MessageFormat ?? _defaults.MessageFormat,
                SignatureAlgorithm     = options.SignatureAlgorithm ?? _defaults.SignatureAlgorithm,
                AdditionalHeaders      = headers,
                RetryableStatusCodes   = _defaults.RetryableStatusCodes,
            };
        }

        private sealed class EffectiveOptions
        {
            public string EndpointUrl { get; init; } = string.Empty;
            public string? SigningSecret { get; init; }
            public int MaxRetryCount { get; init; }
            public TimeSpan RetryDelay { get; init; }
            public double RetryBackoffMultiplier { get; init; }
            public TimeSpan RequestTimeout { get; init; }
            public string? HttpClientName { get; init; }
            public string Format { get; init; } = WebhookMessageFormat.Json;
            public WebhookSignatureAlgorithm SignatureAlgorithm { get; init; }
            public IDictionary<string, string> AdditionalHeaders { get; init; }
                = new Dictionary<string, string>();
            public ISet<int> RetryableStatusCodes { get; init; } = new HashSet<int>();
        }
    }
}
