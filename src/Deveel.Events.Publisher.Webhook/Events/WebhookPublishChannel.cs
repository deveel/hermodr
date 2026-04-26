//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

using System.Net.Http.Headers;

namespace Deveel.Events
{
    /// <summary>
    /// An <see cref="EventPublishChannel{TOptions}"/> and
    /// <see cref="IBatchEventPublishChannel"/> that delivers
    /// <see cref="CloudEvent"/> instances (individually or in batches) to a remote
    /// endpoint via HTTP POST, following webhook best practices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The channel uses <see cref="WebhookPublishOptions"/> as its single options type
    /// for both the channel-level defaults and per-call overrides. On every
    /// <c>PublishAsync</c> / <c>PublishBatchAsync</c> call the per-call overrides are
    /// merged with the channel defaults via <see cref="MergeOptions"/> and the result
    /// is validated by <see cref="EventPublishChannel{TOptions}.ValidateOptions"/>
    /// before the HTTP delivery is attempted.
    /// </para>
    /// <para>
    /// Channel-structural fields (<see cref="WebhookPublishOptions.SignatureHeaderName"/>,
    /// <see cref="WebhookPublishOptions.RetryableStatusCodes"/>, etc.) are always taken
    /// from the channel-level defaults and ignored in per-call overrides.
    /// </para>
    /// </remarks>
    public class WebhookPublishChannel :
        EventPublishChannel<WebhookPublishOptions>,
        IBatchEventPublishChannel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;

        private readonly IDictionary<WebhookSignatureAlgorithm, IWebhookSignatureProvider> _signatureProviders;
        private readonly IDictionary<string, IEventSerializer> _serializers;

        /// <summary>Initializes the channel.</summary>
        /// <param name="options">
        /// Channel-level defaults (endpoint URL, signing secret, retry policy, etc.).
        /// </param>
        /// <param name="httpClientFactory">
        /// The <see cref="IHttpClientFactory"/> used to create named HTTP clients for
        /// each delivery attempt.
        /// </param>
        /// <param name="signatureProviders">
        /// Optional set of <see cref="IWebhookSignatureProvider"/> implementations.
        /// When non-<c>null</c>, any provider whose
        /// <see cref="IWebhookSignatureProvider.Algorithm"/> matches an entry already
        /// registered replaces the built-in default for that algorithm.
        /// </param>
        /// <param name="serializers">
        /// Optional set of <see cref="IEventSerializer"/> implementations.
        /// When non-<c>null</c>, any serializer whose
        /// <see cref="IEventSerializer.Format"/> matches a built-in key replaces the default.
        /// </param>
        /// <param name="validators">
        /// Optional collection of <see cref="IValidateOptions{WebhookPublishOptions}"/>
        /// services registered in the DI container. When the collection is empty or <c>null</c>
        /// validation falls back to DataAnnotations applied to the effective
        /// (merged) <see cref="WebhookPublishOptions"/>.
        /// </param>
        /// <param name="logger">
        /// An optional logger; when <c>null</c> a
        /// <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/> is used.
        /// </param>
        public WebhookPublishChannel(
            IOptions<WebhookPublishOptions> options,
            IHttpClientFactory httpClientFactory,
            IEnumerable<IWebhookSignatureProvider>? signatureProviders = null,
            IEnumerable<IEventSerializer>? serializers = null,
            IEnumerable<IValidateOptions<WebhookPublishOptions>>? validators = null,
            ILogger<WebhookPublishChannel>? logger = null)
            : base(options.Value, validators)
        {
            _httpClientFactory = httpClientFactory;
            _logger            = logger ?? NullLogger<WebhookPublishChannel>.Instance;

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
                [EventMessageFormat.Json]            = JsonEventSerializer.Default,
                [EventMessageFormat.Xml]             = XmlEventSerializer.Default,
                [EventMessageFormat.CloudEventsJson] = CloudEventsJsonSerializer.Default,
                [EventMessageFormat.CloudEventsXml]  = CloudEventsXmlSerializer.Default,
            };
            if (serializers != null)
                foreach (var s in serializers)
                    _serializers[s.Format] = s;
        }

        // ── EventPublishChannel<WebhookPublishOptions> ──────────────────

        /// <summary>
        /// Performs a property-level merge: each nullable delivery field in
        /// <paramref name="perCallOptions"/> that is non-<c>null</c> overrides the
        /// corresponding field from <paramref name="defaults"/>; additional headers are
        /// merged (per-call entries win on key collision).
        /// Channel-structural fields (<see cref="WebhookPublishOptions.SignatureHeaderName"/>,
        /// <see cref="WebhookPublishOptions.RetryableStatusCodes"/>, etc.) are always
        /// copied from <paramref name="defaults"/>.
        /// </summary>
        protected override WebhookPublishOptions MergeOptions(
            WebhookPublishOptions defaults,
            WebhookPublishOptions? perCallOptions)
        {
            if (perCallOptions == null)
                return defaults;

            var mergedHeaders = new Dictionary<string, string>(
                defaults.AdditionalHeaders,
                StringComparer.OrdinalIgnoreCase);
            foreach (var kv in perCallOptions.AdditionalHeaders)
                mergedHeaders[kv.Key] = kv.Value;

            return new WebhookPublishOptions
            {
                // ── Delivery settings: per-call wins when non-null ──────────────
                EndpointUrl            = perCallOptions.EndpointUrl            ?? defaults.EndpointUrl,
                SigningSecret          = perCallOptions.SigningSecret          ?? defaults.SigningSecret,
                MaxRetryCount          = perCallOptions.MaxRetryCount          ?? defaults.MaxRetryCount,
                RetryDelay             = perCallOptions.RetryDelay             ?? defaults.RetryDelay,
                RetryBackoffMultiplier = perCallOptions.RetryBackoffMultiplier ?? defaults.RetryBackoffMultiplier,
                RequestTimeout         = perCallOptions.RequestTimeout         ?? defaults.RequestTimeout,
                MessageFormat          = perCallOptions.MessageFormat          ?? defaults.MessageFormat,
                SignatureAlgorithm     = perCallOptions.SignatureAlgorithm     ?? defaults.SignatureAlgorithm,
                AdditionalHeaders      = mergedHeaders,
                SignatureHeaderName          = defaults.SignatureHeaderName,
                DeliveryIdHeaderName         = defaults.DeliveryIdHeaderName,
                EventTypeHeaderName          = defaults.EventTypeHeaderName,
                TimestampHeaderName          = defaults.TimestampHeaderName,
                SignatureAlgorithmHeaderName  = defaults.SignatureAlgorithmHeaderName,
                RetryableStatusCodes         = defaults.RetryableStatusCodes,
                HttpClientName               = defaults.HttpClientName,
            };
        }

        /// <inheritdoc/>
        protected override Task PublishCoreAsync(
            CloudEvent @event,
            WebhookPublishOptions options,
            CancellationToken cancellationToken)
        {
            var format     = options.MessageFormat ?? EventMessageFormat.Json;
            var serializer = GetSerializer(format);
            var payload    = serializer.Serialize(@event);
            var contentType = serializer.ContentType;

            return DeliverAsync(payload, contentType, eventType: @event.Type, eventCount: 1, options, cancellationToken);
        }

        // ── IBatchEventPublishChannel ────────────────

        Task IBatchEventPublishChannel.PublishBatchAsync(IReadOnlyList<CloudEvent> events, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return PublishBatchAsync(events, options as WebhookPublishOptions, cancellationToken);
        }


        /// <inheritdoc/>
        public Task PublishBatchAsync(
            IReadOnlyList<CloudEvent> events,
            WebhookPublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (events == null || events.Count == 0)
                throw new ArgumentException("The events batch must contain at least one event.", nameof(events));

            var effective = MergeOptions(DefaultOptions, options);
            ValidateOptions(effective);

            var format      = effective.MessageFormat ?? EventMessageFormat.Json;
            var serializer  = GetSerializer(format);
            var payload     = serializer.SerializeBatch(events);
            var contentType = serializer.BatchContentType;

            return DeliverAsync(payload, contentType, eventType: null, eventCount: events.Count, effective, cancellationToken);
        }

        // ── Core delivery ───────────────────────────────────────────────────

        private async Task DeliverAsync(
            byte[] payload,
            string contentType,
            string? eventType,
            int eventCount,
            WebhookPublishOptions options,
            CancellationToken cancellationToken)
        {
            var endpointUrl            = options.EndpointUrl!;
            var maxRetryCount          = options.MaxRetryCount          ?? 3;
            var retryDelay             = options.RetryDelay             ?? TimeSpan.FromSeconds(1);
            var retryBackoffMultiplier = options.RetryBackoffMultiplier ?? 2.0;
            var requestTimeout         = options.RequestTimeout         ?? TimeSpan.FromSeconds(30);
            var signatureAlgorithm     = options.SignatureAlgorithm     ?? WebhookSignatureAlgorithm.HmacSha256;
            var additionalHeaders      = options.AdditionalHeaders;
            var retryableStatusCodes   = options.RetryableStatusCodes;
            var httpClientName         = options.HttpClientName;
            var format                 = options.MessageFormat          ?? EventMessageFormat.Json;

            var deliveryId = Guid.NewGuid().ToString("N");
            var provider   = GetSignatureProvider(signatureAlgorithm);
            var timestamp  = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var algorithm  = signatureAlgorithm.ToString();

            if (eventType != null)
                _logger.LogDeliveringEvent(eventType, deliveryId, format, algorithm, endpointUrl);
            else
                _logger.LogDeliveringBatch(deliveryId, eventCount, format, algorithm, endpointUrl);

            var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = maxRetryCount,
                    UseJitter        = false,
                    DelayGenerator   = args => ValueTask.FromResult<TimeSpan?>(
                        TimeSpan.FromMilliseconds(
                            retryDelay.TotalMilliseconds *
                            Math.Pow(retryBackoffMultiplier, args.AttemptNumber))),
                    ShouldHandle = args =>
                    {
                        if (args.Outcome.Exception != null)
                            return ValueTask.FromResult(
                                args.Outcome.Exception is HttpRequestException ||
                                ((args.Outcome.Exception is TaskCanceledException
                                  or OperationCanceledException)
                                 && !cancellationToken.IsCancellationRequested));

                        if (args.Outcome.Result != null)
                        {
                            var code = (int)args.Outcome.Result.StatusCode;
                            return ValueTask.FromResult(
                                code is >= 500 or 408 ||
                                retryableStatusCodes.Contains(code));
                        }

                        return ValueTask.FromResult(false);
                    },
                    OnRetry = args =>
                    {
                        if (args.Outcome.Exception != null)
                            _logger.LogRetryOnException(
                                deliveryId, args.AttemptNumber + 1,
                                args.Outcome.Exception.Message, args.RetryDelay.TotalMilliseconds);
                        else
                            _logger.LogRetryOnStatusCode(
                                deliveryId, args.AttemptNumber + 1,
                                (int)args.Outcome.Result!.StatusCode, args.RetryDelay.TotalMilliseconds);

                        return ValueTask.CompletedTask;
                    }
                })
                .Build();

            HttpResponseMessage response;
            try
            {
                response = await pipeline.ExecuteAsync(async ct =>
                {
                    using var request = BuildRequest(
                        payload, contentType, eventType, deliveryId, timestamp, provider,
                        endpointUrl, options.SigningSecret, additionalHeaders, options);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(requestTimeout);
                    return await CreateHttpClient(httpClientName).SendAsync(request, cts.Token);
                }, cancellationToken);
            }
            catch (Exception ex)
                when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException
                      && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogDeliveryFailed(deliveryId, maxRetryCount + 1);
                throw new WebhookDeliveryException(
                    $"Webhook delivery {deliveryId} failed after {maxRetryCount + 1} attempt(s).", ex);
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDeliverySucceeded(deliveryId, (int)response.StatusCode);
                return;
            }

            var statusCode = (int)response.StatusCode;

            if (!retryableStatusCodes.Contains(statusCode))
            {
                _logger.LogNonRetryableFailure(deliveryId, statusCode);
                throw new WebhookDeliveryException(
                    $"Webhook delivery {deliveryId} failed with non-retryable status {statusCode}.", statusCode);
            }

            _logger.LogDeliveryExhausted(deliveryId, maxRetryCount + 1);
            throw new WebhookDeliveryException(
                $"Webhook delivery {deliveryId} failed after {maxRetryCount + 1} attempt(s).", statusCode);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private IEventSerializer GetSerializer(string format)
        {
            if (_serializers.TryGetValue(format, out var s)) return s;
            throw new NotSupportedException(
                $"No serializer registered for webhook message format '{format}'.");
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
            string endpointUrl,
            string? signingSecret,
            IDictionary<string, string> additionalHeaders,
            WebhookPublishOptions options)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
            request.Content = new ByteArrayContent(payload);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

            request.Headers.TryAddWithoutValidation(options.DeliveryIdHeaderName, deliveryId);

            if (!string.IsNullOrWhiteSpace(eventType))
                request.Headers.TryAddWithoutValidation(options.EventTypeHeaderName, eventType);

            request.Headers.TryAddWithoutValidation(options.TimestampHeaderName, timestamp.ToString());

            if (!string.IsNullOrWhiteSpace(signingSecret))
            {
                var sig = provider.ComputeSignature(payload, timestamp, signingSecret);
                request.Headers.TryAddWithoutValidation(options.SignatureHeaderName, sig);

                if (!string.IsNullOrWhiteSpace(options.SignatureAlgorithmHeaderName))
                    request.Headers.TryAddWithoutValidation(
                        options.SignatureAlgorithmHeaderName, provider.AlgorithmName);
            }

            foreach (var h in additionalHeaders)
                request.Headers.TryAddWithoutValidation(h.Key, h.Value);

            return request;
        }
    }
}
