//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;
using System.Runtime.ExceptionServices;

using CloudNative.CloudEvents;

using Grpc.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// An <see cref="EventPublishChannel{TOptions}"/> that delivers CloudEvents
    /// to statically-configured gRPC endpoints using
    /// <see href="https://github.com/grpc/grpc-dotnet">grpc-dotnet</see>
    /// (<c>Grpc.Net.Client</c>) for the low-level HTTP/2 client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every publish call fans the event out to <strong>all</strong> configured
    /// endpoints (see <see cref="GrpcPublishOptions.Endpoints"/>) concurrently,
    /// each with its own named <c>GrpcChannel</c> (and therefore its own TLS/mTLS
    /// and <c>CallCredentials</c> configuration via
    /// <see cref="System.Net.Http.IHttpClientFactory"/>).
    /// </para>
    /// <para>
    /// The actual gRPC RPC is delegated to a pluggable
    /// <see cref="IGrpcEventSender"/> strategy: single events are sent via a
    /// unary RPC (<see cref="IGrpcEventSender.SendAsync"/>); batches are sent
    /// via a client-streaming RPC
    /// (<see cref="IGrpcEventSender.SendBatchAsync"/>). The sender calls the
    /// user's <c>.proto</c>-generated gRPC client, keeping the channel
    /// transport-agnostic.
    /// </para>
    /// <para>
    /// Transport failures and non-OK gRPC statuses are surfaced as
    /// <see cref="GrpcPublishException"/> subclasses, so the
    /// <see cref="EventPublisher"/> error-handling pipeline (including the
    /// dead-letter channel) engages automatically.
    /// </para>
    /// </remarks>
    internal class GrpcPublishChannel :
        EventPublishChannel<GrpcPublishOptions>,
        IBatchEventPublishChannel
    {
        private readonly GrpcTransportTelemetry _telemetry = new();
        private readonly IGrpcChannelFactory _channelFactory;
        private readonly IGrpcEventSender _defaultSender;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes the channel.
        /// </summary>
        /// <param name="options">Channel-level defaults (endpoints, ...).</param>
        /// <param name="channelFactory">
        /// The <see cref="IGrpcChannelFactory"/> used to resolve or create
        /// <c>GrpcChannel</c> instances per endpoint.
        /// </param>
        /// <param name="defaultSender">
        /// The default <see cref="IGrpcEventSender"/> used when an endpoint does
        /// not override <see cref="GrpcEndpoint.SenderName"/>.
        /// </param>
        /// <param name="serviceProvider">
        /// The service provider used to lazily resolve named senders and options validators.
        /// </param>
        /// <param name="logger">
        /// An optional logger; when <c>null</c> a
        /// <see cref="NullLogger{T}"/> is used.
        /// </param>
        public GrpcPublishChannel(
            IOptions<GrpcPublishOptions> options,
            IGrpcChannelFactory channelFactory,
            IGrpcEventSender defaultSender,
            IServiceProvider serviceProvider,
            ILogger<GrpcPublishChannel>? logger = null)
            : base(options.Value, serviceProvider)
        {
            _channelFactory = channelFactory;
            _defaultSender = defaultSender;
            _logger = logger ?? NullLogger<GrpcPublishChannel>.Instance;
        }

        /// <inheritdoc/>
        protected override GrpcPublishOptions MergeOptions(
            GrpcPublishOptions defaults,
            GrpcPublishOptions? perCallOptions)
        {
            if (perCallOptions == null)
                return defaults;

            return GrpcPublishOptions.Merge(defaults, perCallOptions);
        }

        /// <inheritdoc/>
        protected override async Task PublishCoreAsync(
            CloudEvent @event,
            GrpcPublishOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var endpoints = options.Endpoints;
            if (endpoints == null || endpoints.Count == 0)
                throw new GrpcPublishException(
                    "Cannot publish an event over gRPC: no endpoints are configured.");

            // Fan-out: deliver to every endpoint concurrently, each with its own
            // GrpcChannel and sender. All endpoints are attempted regardless of
            // individual failures (`Task.WhenAll` does not short-circuit).
            var deliveries = endpoints
                .Select(endpoint => DeliverToEndpointAsync(@event, endpoint, cancellationToken))
                .ToArray();

            await AwaitDeliveriesAsync(deliveries, endpoints.Count, "publish");
        }

        // ── IBatchEventPublishChannel ────────────────

        Task IBatchEventPublishChannel.PublishBatchAsync(
            IReadOnlyList<CloudEvent> events,
            EventPublishOptions? options,
            CancellationToken cancellationToken)
        {
            return PublishBatchAsync(events, options as GrpcPublishOptions, cancellationToken);
        }

        /// <summary>
        /// Publishes a batch of events to all configured gRPC endpoints via
        /// client-streaming RPC.
        /// </summary>
        /// <param name="events">The events to deliver. Must contain at least one event.</param>
        /// <param name="options">
        /// Per-delivery overrides; pass <c>null</c> to use the channel defaults.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task PublishBatchAsync(
            IReadOnlyList<CloudEvent> events,
            GrpcPublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (events == null || events.Count == 0)
                throw new ArgumentException("The events batch must contain at least one event.", nameof(events));

            var effective = MergeOptions(DefaultOptions, options);
            ValidateOptions(effective);

            var endpoints = effective.Endpoints;
            if (endpoints == null || endpoints.Count == 0)
                throw new GrpcPublishException(
                    "Cannot publish a batch over gRPC: no endpoints are configured.");

            var deliveries = endpoints
                .Select(endpoint => DeliverBatchToEndpointAsync(events, endpoint, cancellationToken))
                .ToArray();

            await AwaitDeliveriesAsync(deliveries, endpoints.Count, "batch publish");
        }

        // ── Core delivery ───────────────────────────────────────────────────

        /// <summary>
        /// Awaits all endpoint deliveries, aggregating their failures. Endpoint
        /// faults always take precedence over cancellation (per <see cref="Task.WhenAll"/>
        /// semantics), so a cancelled publish never hides a delivery failure.
        /// </summary>
        /// <param name="deliveries">The per-endpoint delivery tasks.</param>
        /// <param name="endpointCount">The total number of endpoints fanned out to.</param>
        /// <param name="operation">
        /// A label for the operation used in error messages (e.g. <c>publish</c> or
        /// <c>batch publish</c>).
        /// </param>
        private async Task AwaitDeliveriesAsync(
            Task[] deliveries,
            int endpointCount,
            string operation)
        {
            try
            {
                await Task.WhenAll(deliveries);
            }
            catch (Exception ex)
            {
                var failures = CollectDeliveryFailures(deliveries);
                if (failures.Count == 0)
                {
                    // No faulted endpoint task: the publish was cancelled for every
                    // endpoint (Task.WhenAll surfaces cancellation only when no task
                    // faulted) — propagate the cancellation rather than throwing an
                    // empty aggregate.
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }

                _logger.LogPublishFailed(failures.Count, endpointCount);

                // Endpoints cancelled mid-flight are not failures, but they must
                // not silently disappear when other endpoint failures are reported.
                var cancelledCount = deliveries.Count(t => t.IsCanceled);
                if (cancelledCount > 0)
                    _logger.LogDeliveriesCancelledWhileOthersFailed(cancelledCount, endpointCount);

                if (failures.Count == 1)
                    ExceptionDispatchInfo.Capture(failures[0]).Throw();

                throw new GrpcPublishException(
                    $"gRPC {operation} failed for {failures.Count} of {endpointCount} endpoint(s).",
                    failures);
            }
        }

        private static List<Exception> CollectDeliveryFailures(Task[] deliveries)
            => deliveries
                .Where(t => t.IsFaulted)
                .Select(t => t.Exception?.GetBaseException())
                .Where(ex => ex != null)
                .Cast<Exception>()
                .ToList();

        private async Task DeliverToEndpointAsync(
            CloudEvent @event,
            GrpcEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            var address = endpoint.Address;
            using var activity = _telemetry.StartPublishActivity(@event.Type, address, "unary");

            try
            {
                var sender = ResolveSender(endpoint.SenderName, address);
                var deadline = endpoint.Deadline ?? GrpcDefaults.DefaultDeadline;
                var headers = BuildCallHeaders(endpoint);
                var callInvoker = _channelFactory.CreateCallInvoker(address, endpoint.HttpClientName);

                if (IsPlaintextAddress(address))
                    _logger.LogPlaintextDelivery(address);

                _logger.LogDeliveringEvent(@event.Type, address, "unary");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(deadline);

                var context = new GrpcCallContext(
                    callInvoker, headers, DateTime.UtcNow + deadline, cts.Token);

                await sender.SendAsync(@event, context);

                _logger.LogDeliverySucceeded(address);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogDeliveryFailed(address, 1);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw WrapDeliveryException("delivery", address, ex);
            }
        }

        private async Task DeliverBatchToEndpointAsync(
            IReadOnlyList<CloudEvent> events,
            GrpcEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            var address = endpoint.Address;
            var firstType = events[0].Type;
            using var activity = _telemetry.StartPublishActivity(firstType, address, "client_streaming");

            try
            {
                var sender = ResolveSender(endpoint.SenderName, address);
                var deadline = endpoint.Deadline ?? GrpcDefaults.DefaultDeadline;
                var headers = BuildCallHeaders(endpoint);
                var callInvoker = _channelFactory.CreateCallInvoker(address, endpoint.HttpClientName);

                if (IsPlaintextAddress(address))
                    _logger.LogPlaintextDelivery(address);

                _logger.LogDeliveringEvent(firstType, address, "client_streaming");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(deadline);

                var context = new GrpcCallContext(
                    callInvoker, headers, DateTime.UtcNow + deadline, cts.Token);

                await sender.SendBatchAsync(events, context);

                _logger.LogDeliverySucceeded(address);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogDeliveryFailed(address, 1);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw WrapDeliveryException("batch delivery", address, ex);
            }
        }

        /// <summary>
        /// Resolves the sender for an endpoint: the default sender when no name is
        /// configured, otherwise the keyed sender with that name. A configured name
        /// that is not registered fails the delivery (fail-fast) instead of silently
        /// falling back to the default sender, which could misdirect events to the
        /// wrong gRPC service.
        /// </summary>
        private IGrpcEventSender ResolveSender(string? senderName, string address)
        {
            if (string.IsNullOrEmpty(senderName))
                return _defaultSender;

            var keyed = ServiceProvider.GetKeyedService<IGrpcEventSender>(senderName);
            if (keyed is not null)
                return keyed;

            throw new GrpcPublishException(
                $"No IGrpcEventSender named '{senderName}' is registered in the dependency injection container, " +
                $"but the gRPC endpoint '{address}' requires it. Register a named sender via " +
                "AddGrpcEventSender<TSender>(name) on the EventPublisherBuilder, or remove the " +
                "endpoint's SenderName to use the default sender.");
        }

        private static Metadata BuildCallHeaders(GrpcEndpoint endpoint)
        {
            var metadata = new Metadata();
            foreach (var header in endpoint.Headers)
                metadata.Add(header.Key, header.Value);
            return metadata;
        }

        private static bool IsPlaintextAddress(string address) =>
            Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Classifies a per-endpoint delivery failure: genuine transport/network
        /// failures become <see cref="GrpcTransportException"/> (preserving the gRPC
        /// status code), while configuration or sender-code failures become the base
        /// <see cref="GrpcPublishException"/> so callers can distinguish them. Failures
        /// that are already <see cref="GrpcPublishException"/> instances are passed
        /// through unchanged.
        /// </summary>
        private static Exception WrapDeliveryException(string operationLabel, string address, Exception ex)
        {
            if (ex is GrpcPublishException)
                return ex;

            return ex switch
            {
                RpcException or HttpRequestException or TimeoutException => new GrpcTransportException(
                    $"gRPC {operationLabel} to '{address}' failed: {ex.Message}.", ex),
                OperationCanceledException => new GrpcTransportException(
                    $"gRPC {operationLabel} to '{address}' was cancelled before completion (deadline or transport timeout): {ex.Message}.", ex),
                _ => new GrpcPublishException(
                    $"gRPC {operationLabel} to '{address}' failed with an unexpected error: {ex.Message}.", ex),
            };
        }
    }
}
