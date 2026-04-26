//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// A service that is used to publish events
    /// to one or more channels.
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Publishes the given event to the underlying channels.
        /// </summary>
        /// <param name="event">
        /// The instance of the <see cref="CloudEvent"/> to publish.
        /// </param>
        /// <param name="options">
        /// Optional per-call publish options passed to each channel.  Channels that do not
        /// recognise the concrete options type will receive <c>null</c> and fall back to
        /// their registered defaults.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to cancel the operation.
        /// </param>
        /// <returns>
        /// Returns a task that will be completed when the event is published.
        /// </returns>
        Task PublishEventAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default);
    }
}
