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
        /// The options to use for this publish operation, which can override
        /// the channel-level defaults.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to cancel the operation.
        /// </param>
        /// <returns>
        /// Returns a task that represents the asynchronous operation.
        /// </returns>
		Task PublishAsync(CloudEvent @event, EventPublishChannelOptions? options = null, CancellationToken cancellationToken = default);
	}
}
