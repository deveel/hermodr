//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;
using System.Net.Http.Headers;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

namespace Hermodr
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
    class WebhookPublishChannel :
        EventPublishChannel<WebhookPublishOptions>,
        IBatchEventPublishChannel
    {
        // Key used to flow the per-delivery ID through the ResilienceContext
        // so that OnRetry callbacks can log it without capturing it in a closure.
        private static readonly ResiliencePropertyKey<string> DeliveryIdKey =
            new("webhook.deliveryId");

        private readonly WebhookTransportTelemetry _telemetry = new();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IEventSystemTime _systemTime;
        private readonly ILogger _logger;

        private readonly WebhookHandshakeClient _handshakeClient;

        private IDictionary<WebhookSignatureAlgorithm, IWebhookSignatureProvider>? _signatureProviders;
        private IDictionary<string, IEventSerializer>? _serializers;

        // Cached pipeline built from the channel-level default retry options.
        // Avoids rebuilding the Polly pipeline on every delivery when per-call
        // options don't change the retry behaviour.
        private readonly Lazy<ResiliencePipeline<HttpResponseMessage>> _defaultPipeline;

        /// <summary>Initializes the channel.</summary>
        /// <param name="options">
        /// Channel-level defaults (endpoint URL, signing secret, retry policy, etc.).
        /// </param>
        /// <param name="httpClientFactory">
        /// The <see cref="IHttpClientFactory"/> used to create named HTTP clients for
        /// each delivery attempt.
        /// </param>
        /// <param name="serviceProvider">
        /// The service provider used to lazily resolve serializers and signature providers.
        /// </param>
        /// <param name="logger">
        /// An optional logger; when <c>null</c> a
        /// <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/> is used.
        /// </param>
        public WebhookPublishChannel(
            IOptions<WebhookPublishOptions> options,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider,
            ILogger<WebhookPublishChannel>? logger = null)
            : base(options.Value, serviceProvider)
        {
            _httpClientFactory = httpClientFactory;
            _systemTime        = EventSystemTime.Instance;
            _logger            = logger ?? NullLogger<WebhookPublishChannel>.Instance;

            // The WebHook discovery handshake client is owned by the channel so
            // that the per-endpoint permission cache persists across deliveries.
            _handshakeClient = new WebhookHandshakeClient(httpClientFactory, logger);

            // Build the default pipeline lazily from the channel-level options.
            // LazyThreadSafetyMode.ExecutionAndPublication guarantees exactly-once
            // construction even under concurrent first-use.
            _defaultPipeline = new Lazy<ResiliencePipeline<HttpResponseMessage>>(
                () => BuildRetryPipeline(
                    options.Value.MaxRetryCount          ?? 3,
                    options.Value.RetryDelay             ?? TimeSpan.FromSeconds(1),
                    options.Value.RetryBackoffMultiplier ?? 2.0,
                    options.Value.RetryableStatusCodes),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        // ── EventPublishChannel<WebhookPublishOptions> ──────────────────

        private IEventSystemTime SystemTime => ServiceProvider.GetRequiredService<IEventSystemTime>();

        private IDictionary<WebhookSignatureAlgorithm, IWebhookSignatureProvider> SignatureProviders
        {
            get
            {
                if (_signatureProviders == null)
                {
                    var providers = new Dictionary<WebhookSignatureAlgorithm, IWebhookSignatureProvider>
                    {
                        [WebhookSignatureAlgorithm.HmacSha256] = HmacSha256SignatureProvider.Default,
                        [WebhookSignatureAlgorithm.HmacSha384] = HmacSha384SignatureProvider.Default,
                        [WebhookSignatureAlgorithm.HmacSha512] = HmacSha512SignatureProvider.Default,
#pragma warning disable CS0618
                        [WebhookSignatureAlgorithm.HmacSha1]   = HmacSha1SignatureProvider.Default,
#pragma warning restore CS0618
                    };
                    foreach (var p in ServiceProvider.GetServices<IWebhookSignatureProvider>())
                        providers[p.Algorithm] = p;
                    _signatureProviders = providers;
                }
                return _signatureProviders;
            }
        }

        private IDictionary<string, IEventSerializer> Serializers
        {
            get
            {
                if (_serializers == null)
                {
                    var serializers = new Dictionary<string, IEventSerializer>(StringComparer.OrdinalIgnoreCase)
                    {
                        [EventMessageFormat.Json]              = JsonEventSerializer.Default,
                        [EventMessageFormat.Xml]               = XmlEventSerializer.Default,
                        [EventMessageFormat.CloudEventsJson]   = CloudEventsJsonSerializer.Default,
                        [EventMessageFormat.CloudEventsXml]    = CloudEventsXmlSerializer.Default,
                        [EventMessageFormat.CloudEventsBinary] = CloudEventsBinarySerializer.Default,
                    };
                    foreach (var s in ServiceProvider.GetServices<IEventSerializer>())
                        serializers[s.Format] = s;
                    _serializers = serializers;
                }
                return _serializers;
            }
        }

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
                ScheduleDeliveryAt     = perCallOptions.ScheduleDeliveryAt     ?? defaults.ScheduleDeliveryAt,
                Discovery              = perCallOptions.Discovery              ?? defaults.Discovery,
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

            // Binary content mode: the body is the raw data and the Content-Type
            // is the event's datacontenttype; the CloudEvent attributes are
            // projected onto ce-* HTTP headers by CloudEventHttpHeadersMapper.
            IReadOnlyDictionary<string, string>? ceHeaders = null;
            var contentType = serializer.ContentType;

            if (string.Equals(format, EventMessageFormat.CloudEventsBinary,
                    StringComparison.OrdinalIgnoreCase))
            {
                contentType = !string.IsNullOrWhiteSpace(@event.DataContentType)
                    ? @event.DataContentType!
                    : serializer.ContentType;
                ceHeaders = CloudEventHttpHeadersMapper.Map(@event);
            }

            return DeliverAsync(payload, contentType, eventType: @event.Type, eventCount: 1,
                @event.Time, options, cancellationToken, ceHeaders);
        }

        // ── IBatchEventPublishChannel ────────────────

        Task IBatchEventPublishChannel.PublishBatchAsync(IReadOnlyList<CloudEvent> events, EventPublishOptions? options,
            CancellationToken cancellationToken)
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

            var format = effective.MessageFormat ?? EventMessageFormat.Json;

            // Binary content mode is defined by the CloudEvents HTTP binding
            // specification only for single-event delivery.
            if (string.Equals(format, EventMessageFormat.CloudEventsBinary,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    "Binary content mode is not defined for batches by the CloudEvents HTTP binding specification.");
            }

            var serializer  = GetSerializer(format);
            var payload     = serializer.SerializeBatch(events);
            var contentType = serializer.BatchContentType;

            return DeliverAsync(payload, contentType, eventType: null, eventCount: events.Count,
                eventTime: null, effective, cancellationToken, ceHeaders: null);
        }

        // ── Core delivery ───────────────────────────────────────────────────

        private async Task DeliverAsync(
            byte[] payload,
            string contentType,
            string? eventType,
            int eventCount,
            DateTimeOffset? eventTime,
            WebhookPublishOptions options,
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, string>? ceHeaders = null)
        {
            var endpointUrl            = options.EndpointUrl!;

            using var activity = _telemetry.StartPublishActivity(eventType, endpointUrl);
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
            var timestamp  = (eventTime ?? SystemTime.UtcNow).ToUnixTimeSeconds();
            var algorithm  = signatureAlgorithm.ToString();

            // ── CloudEvents HTTP WebHooks abuse-protection handshake ──────────
            // When Discovery is configured, lazily perform the OPTIONS pre-flight
            // validation request (cached per endpoint) and capture the granted
            // permission for telemetry/metadata. The WebHook-Request-Origin
            // header is then attached to every delivery POST per §2.1.
            WebhookDiscoveryPermission? permission = null;
            string? webHookRequestOrigin = null;

            if (options.Discovery is WebhookDiscoveryOptions discovery)
            {
                permission = await _handshakeClient.EnsureHandshakeAsync(
                    endpointUrl, discovery, httpClientName, cancellationToken)
                    .ConfigureAwait(false);

                webHookRequestOrigin = discovery.RequestOrigin;

                if (permission is not null)
                {
                    activity?.SetTag("webhook.allowed_origin", permission.AllowedOrigin);
                    if (permission.AllowedRate is int ar)
                        activity?.SetTag("webhook.allowed_rate", ar);
                }
            }

            if (eventType != null)
                _logger.LogDeliveringEvent(eventType, deliveryId, format, algorithm, endpointUrl);
            else
                _logger.LogDeliveringBatch(deliveryId, eventCount, format, algorithm, endpointUrl);

            // Use the channel-default cached pipeline when the effective retry parameters
            // match the channel defaults; build a fresh one only for per-call overrides.
            var defaultOpts = DefaultOptions;
            var matchesDefault =
                maxRetryCount          == (defaultOpts.MaxRetryCount          ?? 3)          &&
                retryDelay             == (defaultOpts.RetryDelay             ?? TimeSpan.FromSeconds(1)) &&
                retryBackoffMultiplier == (defaultOpts.RetryBackoffMultiplier ?? 2.0)        &&
                ReferenceEquals(retryableStatusCodes, defaultOpts.RetryableStatusCodes);

            var pipeline = matchesDefault
                ? _defaultPipeline.Value
                : BuildRetryPipeline(maxRetryCount, retryDelay, retryBackoffMultiplier, retryableStatusCodes);

            // Flow the delivery ID into the pipeline via ResilienceContext so that OnRetry
            // can log it without capturing a per-call closure variable.
            var resilienceContext = ResilienceContextPool.Shared.Get(cancellationToken);
            resilienceContext.Properties.Set(DeliveryIdKey, deliveryId);

            HttpResponseMessage response;
            try
            {
                response = await pipeline.ExecuteAsync(async ctx =>
                {
                    using var request = BuildRequest(
                        payload, contentType, eventType, deliveryId, timestamp, provider,
                        endpointUrl, options.SigningSecret, additionalHeaders, options,
                        ceHeaders, webHookRequestOrigin);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
                    cts.CancelAfter(requestTimeout);
                    return await CreateHttpClient(httpClientName).SendAsync(request, cts.Token);
                }, resilienceContext);
            }
            catch (Exception ex)
                when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException
                      && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogDeliveryFailed(deliveryId, maxRetryCount + 1);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw new WebhookTransportException(
                    $"Webhook delivery {deliveryId} failed after {maxRetryCount + 1} attempt(s).", ex);
            }
            finally
            {
                ResilienceContextPool.Shared.Return(resilienceContext);
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDeliverySucceeded(deliveryId, (int)response.StatusCode);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            var statusCode = (int)response.StatusCode;

            if (!retryableStatusCodes.Contains(statusCode))
            {
                _logger.LogNonRetryableFailure(deliveryId, statusCode);
                throw new WebhookStatusCodeException(
                    $"Webhook delivery {deliveryId} failed with non-retryable status {statusCode}.", statusCode);
            }

            _logger.LogDeliveryExhausted(deliveryId, maxRetryCount + 1);
            throw new WebhookStatusCodeException(
                $"Webhook delivery {deliveryId} failed after {maxRetryCount + 1} attempt(s).", statusCode);
        }

        /// <summary>
        /// Builds a <see cref="ResiliencePipeline{T}"/> from explicit retry parameters.
        /// The pipeline does not capture any per-call state; per-delivery information
        /// (e.g. delivery ID) is read from <see cref="ResilienceContext.Properties"/>
        /// at runtime via <see cref="DeliveryIdKey"/>.
        /// </summary>
        private ResiliencePipeline<HttpResponseMessage> BuildRetryPipeline(
            int maxRetryCount,
            TimeSpan retryDelay,
            double retryBackoffMultiplier,
            ISet<int> retryableStatusCodes)
        {
            return new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = maxRetryCount,
                    UseJitter        = false,
                    // Per the CloudEvents HTTP WebHooks spec §2.2, a 429 response
                    // with Retry-After MUST be honored. When the response carries
                    // a Retry-After header (delta-seconds or HTTP-date) we use it
                    // as the next retry delay; otherwise we fall back to the
                    // configured exponential backoff.
                    DelayGenerator = args =>
                    {
                        var retryAfter = TryGetRetryAfterDelay(args.Outcome.Result);
                        if (retryAfter is TimeSpan ra)
                            return ValueTask.FromResult<TimeSpan?>(ra);

                        var backoff = TimeSpan.FromMilliseconds(
                            retryDelay.TotalMilliseconds *
                            Math.Pow(retryBackoffMultiplier, args.AttemptNumber));
                        return ValueTask.FromResult<TimeSpan?>(backoff);
                    },
                    // External cancellation is handled by Polly respecting the
                    // CancellationToken in the ResilienceContext; only transport
                    // errors and server-side transient failures are retried here.
                    ShouldHandle = args =>
                    {
                        if (args.Outcome.Exception != null)
                            return ValueTask.FromResult(
                                args.Outcome.Exception is HttpRequestException ||
                                args.Outcome.Exception is TaskCanceledException or OperationCanceledException);

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
                        // Read the delivery ID from the context properties to avoid
                        // closing over per-call variables in a cached pipeline.
                        args.Context.Properties.TryGetValue(DeliveryIdKey, out var id);
                        if (args.Outcome.Exception != null)
                            _logger.LogRetryOnException(
                                id ?? "unknown", args.AttemptNumber + 1,
                                args.Outcome.Exception.Message, args.RetryDelay.TotalMilliseconds);
                        else
                            _logger.LogRetryOnStatusCode(
                                id ?? "unknown", args.AttemptNumber + 1,
                                (int)args.Outcome.Result!.StatusCode, args.RetryDelay.TotalMilliseconds);

                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private IEventSerializer GetSerializer(string format)
        {
            if (Serializers.TryGetValue(format, out var s)) return s;
            throw new NotSupportedException(
                $"No serializer registered for webhook message format '{format}'.");
        }

        private IWebhookSignatureProvider GetSignatureProvider(WebhookSignatureAlgorithm alg)
        {
            if (SignatureProviders.TryGetValue(alg, out var p)) return p;
            throw new NotSupportedException($"No signature provider registered for algorithm '{alg}'.");
        }

        private HttpClient CreateHttpClient(string? name)
            => _httpClientFactory.CreateClient(
                string.IsNullOrEmpty(name) ? WebhookDefaults.HttpClientName : name);

        /// <summary>
        /// Parses the <c>Retry-After</c> response header (RFC 7231 §7.1.3) into
        /// a delay. Supports both delta-seconds and HTTP-date forms. Returns
        /// <c>null</c> when the header is absent or unparseable.
        /// </summary>
        private static TimeSpan? TryGetRetryAfterDelay(HttpResponseMessage? response)
        {
            if (response is null)
                return null;

            if (!response.Headers.TryGetValues("Retry-After", out var values))
                return null;

            var raw = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            raw = raw.Trim();

            // Form 1: delta-seconds (non-negative integer).
            if (int.TryParse(raw, out var seconds) && seconds >= 0)
                return TimeSpan.FromSeconds(Math.Min(seconds, 3600));

            // Form 2: HTTP-date.
            if (DateTimeOffset.TryParseExact(raw, "R", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var date))
            {
                var delta = date - DateTimeOffset.UtcNow;
                return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
            }

            return null;
        }

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
            WebhookPublishOptions options,
            IReadOnlyDictionary<string, string>? cloudEventHeaders = null,
            string? webHookRequestOrigin = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
            request.Content = new ByteArrayContent(payload);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

            // CloudEvents binary content mode: ce-* attribute headers are emitted
            // first so that explicit X-Webhook-* headers and AdditionalHeaders can
            // still override them on key collision (the spec mandates ce-* names,
            // but the channel preserves the existing webhook header semantics).
            if (cloudEventHeaders is not null)
            {
                foreach (var h in cloudEventHeaders)
                    request.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            // CloudEvents HTTP WebHooks §2.1: when the target requires abuse
            // protection, every delivery POST carries WebHook-Request-Origin
            // (with the value matching the one used in the OPTIONS handshake).
            if (!string.IsNullOrWhiteSpace(webHookRequestOrigin))
                request.Headers.TryAddWithoutValidation(
                    WebhookHandshakeClient.RequestOriginHeader, webHookRequestOrigin);

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
