//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// A pluggable strategy that performs the actual gRPC call to publish a
    /// single <see cref="CloudEvent"/> (unary RPC) or a batch of events
    /// (client-streaming RPC) against a user-supplied, <c>.proto</c>-generated
    /// gRPC service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="GrpcPublishChannel"/> owns all transport concerns
    /// (fan-out, <c>GrpcChannel</c> lifetime, TLS/mTLS, error mapping,
    /// telemetry, middleware/dead-letter integration) and delegates only the
    /// RPC invocation to this interface. The sender receives a
    /// <see cref="GrpcCallContext"/> carrying a <see cref="Grpc.Core.CallInvoker"/>
    /// (created from the per-endpoint <c>GrpcChannel</c>), the per-endpoint call
    /// headers, the deadline, and the cancellation token.
    /// </para>
    /// <para>
    /// A typical sender constructs its generated gRPC client from the
    /// <see cref="Grpc.Core.CallInvoker"/> and forwards the context values to
    /// the RPC call, for example:
    /// <code>
    /// var client = new OrderPubClient(context.CallInvoker);
    /// await client.PublishAsync(request,
    ///     headers: context.Headers,
    ///     deadline: context.Deadline,
    ///     cancellationToken: context.CancellationToken);
    /// </code>
    /// </para>
    /// <para>
    /// A single sender service is registered for the channel (see
    /// <c>AddGrpcEventSender&lt;T&gt;()</c>); when an endpoint overrides
    /// <see cref="GrpcEndpoint.SenderName"/> the channel resolves a named
    /// <see cref="IGrpcEventSender"/> instead.
    /// </para>
    /// <para>
    /// A built-in sender targeting the canonical CloudEvents protobuf format
    /// is planned for a future release alongside the <c>.proto</c> generator
    /// (roadmap v1.6.0). Until then, callers register their own
    /// <see cref="IGrpcEventSender"/> implementation backed by their generated
    /// gRPC client.
    /// </para>
    /// </remarks>
    public interface IGrpcEventSender
    {
        /// <summary>
        /// Publishes a single <paramref name="event"/> via a unary RPC call
        /// using the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="event">The CloudEvent to deliver.</param>
        /// <param name="context">
        /// The per-call gRPC context (call invoker, headers, deadline,
        /// cancellation token).
        /// </param>
        /// <returns>A task that completes when the RPC has been acknowledged.</returns>
        Task SendAsync(CloudEvent @event, GrpcCallContext context);

        /// <summary>
        /// Publishes a batch of <paramref name="events"/> via a client-streaming
        /// RPC call using the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="events">The events to deliver. Must contain at least one event.</param>
        /// <param name="context">
        /// The per-call gRPC context (call invoker, headers, deadline,
        /// cancellation token).
        /// </param>
        /// <returns>A task that completes when the streaming RPC has been acknowledged.</returns>
        Task SendBatchAsync(IReadOnlyList<CloudEvent> events, GrpcCallContext context);
    }
}
