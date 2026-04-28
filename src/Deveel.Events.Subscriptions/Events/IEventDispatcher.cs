//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Dispatches a <see cref="CloudEvent"/> to the matching subscribers held in the
    /// <see cref="IEventSubscriptionRegistry"/>.
    /// </summary>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Routes <paramref name="event"/> to every subscription whose filter matches the event,
        /// invoking each handler in sequence.
        /// </summary>
        /// <param name="event">The event to dispatch.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task DispatchAsync(CloudEvent @event, CancellationToken cancellationToken = default);
    }
}

