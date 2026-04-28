//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Represents a subscription to a specific class of <see cref="CloudEvent"/>s,
    /// identified by a <see cref="Filter"/> and serviced by a handler.
    /// </summary>
    public interface IEventSubscription
    {
        /// <summary>
        /// Gets an optional name used to identify this subscription in logs and audit trails.
        /// </summary>
        string? Name { get; }

        /// <summary>
        /// Gets the filter that determines which <see cref="CloudEvent"/>s this subscription
        /// is interested in.
        /// </summary>
        EventFilter Filter { get; }

        /// <summary>
        /// Invokes the subscription handler with the supplied <paramref name="event"/>.
        /// </summary>
        /// <param name="event">The matched event to handle.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task HandleAsync(CloudEvent @event, CancellationToken cancellationToken = default);
    }
}

