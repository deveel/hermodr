//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// An <see cref="EventPublishChannel{TOptions}"/> that delivers CloudEvents to
    /// statically-configured HTTP endpoints using the
    /// <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md">CloudEvents HTTP binding</see>
    /// (structured or binary content mode).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every publish call fans the event out to <strong>all</strong> configured
    /// endpoints (see <see cref="HttpPublishOptions.Endpoints"/>) concurrently, each
    /// with its own named <see cref="HttpClient"/> (and therefore its own
    /// authentication handlers and standard resilience pipeline). There is no
    /// subscriber registry, no signing and no delivery-receipt model: the channel is
    /// the lightweight, trust-based complement to the <c>Hermodr.Publisher.Webhook</c>
    /// package, which owns dynamic subscriber management and signed delivery.
    /// </para>
    /// <para>
    /// The body format is selected per endpoint via <see cref="HttpEndpoint.Format"/>:
    /// the structured content modes (<c>cloudevents+json</c>, <c>cloudevents+xml</c>)
    /// embed the whole CloudEvent envelope in the request body, while the binary
    /// content mode (<c>cloudevents+binary</c>) sends the raw event <c>data</c> as the
    /// body — with its native <c>datacontenttype</c> as <c>Content-Type</c> — and the
    /// context attributes as <c>ce-*</c> headers.
    /// </para>
    /// <para>
    /// Transport failures and non-success status codes are surfaced as
    /// <see cref="HttpPublishException"/> subclasses, so the
    /// <see cref="EventPublisher"/> error-handling pipeline (including the
    /// dead-letter channel) engages automatically.
    /// </para>
    /// </remarks>
    internal class HttpPublishChannel :
        EventPublishChannel<HttpPublishOptions>
    {
        private readonly HttpTransportTelemetry _telemetry = new();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;

        private IDictionary<string, IEventSerializer>? _serializers;

        /// <summary>
        /// Initializes the channel.
        /// </summary>
        /// <param name="options">Channel-level defaults (endpoints, formats, ...).</param>
        /// <param name="httpClientFactory">
        /// The <see cref="IHttpClientFactory"/> used to resolve the named
        /// <see cref="HttpClient"/> for each endpoint.
        /// </param>
        /// <param name="serviceProvider">
        /// The service provider used to lazily resolve serializers and options validators.
        /// </param>
        /// <param name="logger">
        /// An optional logger; when <c>null</c> a
        /// <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/> is used.
        /// </param>
        public HttpPublishChannel(
            IOptions<HttpPublishOptions> options,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider,
            ILogger<HttpPublishChannel>? logger = null)
            : base(options.Value, serviceProvider)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger ?? NullLogger<HttpPublishChannel>.Instance;
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

        /// <inheritdoc/>
        protected override HttpPublishOptions MergeOptions(
            HttpPublishOptions defaults,
            HttpPublishOptions? perCallOptions)
        {
            if (perCallOptions == null)
                return defaults;

            return HttpPublishOptions.Merge(defaults, perCallOptions);
        }

        /// <inheritdoc/>
        protected override async Task PublishCoreAsync(
            CloudEvent @event,
            HttpPublishOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var endpoints = options.Endpoints;
            if (endpoints == null || endpoints.Count == 0)
                throw new HttpPublishException(
                    "Cannot publish an event over HTTP: no endpoints are configured.");

            // Fan-out: deliver to every endpoint concurrently, each with its own
            // resilience and authentication pipeline. All endpoints are attempted
            // regardless of individual failures (`Task.WhenAll` does not short-circuit).
            var deliveries = endpoints
                .Select(endpoint => DeliverToEndpointAsync(@event, endpoint, cancellationToken))
                .ToArray();

            try
            {
                await Task.WhenAll(deliveries);
            }
            catch
            {
                var failures = deliveries
                    .Where(t => t.IsFaulted)
                    .Select(t => t.Exception?.GetBaseException())
                    .Where(ex => ex != null)
                    .Cast<Exception>()
                    .ToList();

                _logger.LogPublishFailed(failures.Count, endpoints.Count);

                if (failures.Count == 1)
                    ExceptionDispatchInfo.Capture(failures[0]).Throw();

                throw new HttpPublishException(
                    $"HTTP publish failed for {failures.Count} of {endpoints.Count} endpoint(s).",
                    failures);
            }
        }

        private async Task DeliverToEndpointAsync(
            CloudEvent @event,
            HttpEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            var url = endpoint.Url;
            var format     = endpoint.Format ?? HttpDefaults.DefaultFormat;

            using var activity = _telemetry.StartPublishActivity(@event.Type, url);

            var requestTimeout = endpoint.RequestTimeout ?? HttpDefaults.DefaultRequestTimeout;
            var serializer     = GetSerializer(format);
            var payload        = serializer.Serialize(@event);
            var contentType    = serializer.ContentType;

            // CloudEvents binary content mode: the body is the raw data and the
            // Content-Type is the event's datacontenttype; the CloudEvent attributes
            // are projected onto ce-* HTTP headers.
            IReadOnlyDictionary<string, string>? ceHeaders = null;
            if (string.Equals(format, EventMessageFormat.CloudEventsBinary,
                    StringComparison.OrdinalIgnoreCase))
            {
                contentType = !string.IsNullOrWhiteSpace(@event.DataContentType)
                    ? @event.DataContentType!
                    : serializer.ContentType;
                ceHeaders = CloudEventHttpHeadersMapper.Map(@event);
            }

            _logger.LogDeliveringEvent(@event.Type ?? "unknown", format, url);

            Exception? transportFailure = null;
            HttpResponseMessage? response = null;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(requestTimeout);

                using var request = BuildRequest(@event, payload, endpoint, contentType, ceHeaders);
                response = await CreateHttpClient(endpoint.HttpClientName)
                    .SendAsync(request, cts.Token);
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                transportFailure = ex;
            }

            if (transportFailure != null)
            {
                _logger.LogDeliveryFailed(url, 1);
                activity?.SetStatus(ActivityStatusCode.Error, transportFailure.Message);
                throw new HttpTransportException(
                    $"HTTP delivery to '{url}' failed: {transportFailure.Message}.", transportFailure);
            }

            if (response!.IsSuccessStatusCode)
            {
                _logger.LogDeliverySucceeded(url, (int)response.StatusCode);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            var statusCode = (int)response.StatusCode;
            _logger.LogNonRetryableFailure(url, statusCode);
            activity?.SetStatus(ActivityStatusCode.Error, $"HTTP status {statusCode}");

            throw new HttpStatusCodeException(
                $"HTTP delivery to '{url}' failed with status {statusCode}.", statusCode);
        }

        private IEventSerializer GetSerializer(string format)
        {
            if (Serializers.TryGetValue(format, out var serializer))
                return serializer;

            throw new NotSupportedException(
                $"No serializer registered for HTTP message format '{format}'.");
        }

        private HttpClient CreateHttpClient(string? name)
            => _httpClientFactory.CreateClient(
                string.IsNullOrEmpty(name) ? HttpDefaults.HttpClientName : name);

        private HttpRequestMessage BuildRequest(
            CloudEvent @event,
            byte[] payload,
            HttpEndpoint endpoint,
            string contentType,
            IReadOnlyDictionary<string, string>? cloudEventHeaders)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url);
            request.Content = new ByteArrayContent(payload);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

            // CloudEvents binary content mode: emit the ce-* attribute headers first
            // so that AdditionalHeaders can still override them on key collision.
            if (cloudEventHeaders is not null)
            {
                foreach (var header in cloudEventHeaders)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var header in endpoint.AdditionalHeaders)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return request;
        }
    }
}