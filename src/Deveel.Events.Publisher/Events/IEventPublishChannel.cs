//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events {
    /// <summary>
    /// Represents a channel that can be used to publish events.
    /// </summary>
    public interface IEventPublishChannel {
        /// <summary>
        /// Publishes the given event to the channel.
        /// </summary>
        /// <param name="event">
        /// The event to be published.
        /// </param>
        /// <param name="options">
        /// Optional per-call publish options that override the channel-level defaults.
        /// When <c>null</c> the channel uses its registered defaults.
        /// If the options object is not compatible with the options type expected by the
        /// channel, the channel treats this as <c>null</c> and falls back to its defaults.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to cancel the operation.
        /// </param>
        /// <returns>
        /// Returns a task that represents the asynchronous operation.
        /// </returns>
		Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes the given event to the channel using the channel's registered defaults.
        /// </summary>
        /// <param name="event">The event to be published.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>Returns a task that represents the asynchronous operation.</returns>
        Task PublishAsync(CloudEvent @event, CancellationToken cancellationToken)
            => PublishAsync(@event, null, cancellationToken);
	}
}
