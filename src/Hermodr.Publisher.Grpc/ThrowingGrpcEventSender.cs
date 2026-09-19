//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// A fallback <see cref="IGrpcEventSender"/> registered by
    /// <c>AddGrpcEventPublisherChannel</c> when no custom sender has been
    /// registered. It throws with a helpful message directing the caller to
    /// register an <see cref="IGrpcEventSender"/> via
    /// <c>AddGrpcEventSender&lt;T&gt;()</c>.
    /// </summary>
    /// <remarks>
    /// This sender is registered with <c>TryAddSingleton</c> so the first user
    /// registration of <see cref="IGrpcEventSender"/> replaces it.
    /// A built-in sender targeting the canonical CloudEvents protobuf format
    /// is planned for v1.6.0 alongside the <c>.proto</c> generator.
    /// </remarks>
    internal sealed class ThrowingGrpcEventSender : IGrpcEventSender
    {
        private static InvalidOperationException NoSenderRegistered() => new(
            "No IGrpcEventSender is registered. Register one via " +
            "AddGrpcEventSender<TSender>() on the EventPublisherBuilder, " +
            "providing an implementation that calls your .proto-generated " +
            "gRPC client. A built-in CloudEvents-protobuf sender is planned " +
            "for v1.6.0.");

        /// <inheritdoc/>
        public Task SendAsync(CloudEvent @event, GrpcCallContext context)
            => throw NoSenderRegistered();

        /// <inheritdoc/>
        public Task SendBatchAsync(IReadOnlyList<CloudEvent> events, GrpcCallContext context)
            => throw NoSenderRegistered();
    }
}
