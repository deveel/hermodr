//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr {
    /// <summary>
    /// A callback that is invoked when an event is published.
    /// </summary>
    public interface IEventPublishCallback {
        /// <summary>
        /// Invoked when an event is published.
        /// </summary>
        /// <param name="event">The event that was published.</param>
        /// <returns>
        /// Returns a <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
		Task OnEventPublishedAsync(CloudEvent @event);
	}
}
