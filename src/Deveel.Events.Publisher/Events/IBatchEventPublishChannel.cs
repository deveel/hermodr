//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// An <see cref="IEventPublishChannel"/> that also supports delivering
    /// multiple events in a single message (batch delivery).
    /// </summary>
    public interface IBatchEventPublishChannel : IEventPublishChannel
    {
        /// <summary>
        /// Publishes a batch of events using the channel-level defaults.
        /// </summary>
        /// <param name="events">The events to deliver. Must not be empty.</param>
        /// <param name="options">
        /// The options to use for this batch delivery. If not specified, the channel-level
        /// defaults will be used.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task PublishBatchAsync(
            IReadOnlyList<CloudEvent> events,
            EventPublishOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}
