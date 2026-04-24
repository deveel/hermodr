//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Extends <see cref="IEventPublishChannel"/> with a typed per-delivery options
    /// parameter, allowing callers to override channel defaults on a per-call basis.
    /// </summary>
    /// <typeparam name="TOptions">
    /// The options type accepted by this channel alongside a <see cref="CloudEvent"/>.
    /// </typeparam>
    public interface IEventPublishChannel<in TOptions> : IEventPublishChannel
        where TOptions : class
    {
        /// <summary>
        /// Publishes the given event to the channel, applying <paramref name="options"/>
        /// on top of the channel-level defaults for this delivery only.
        /// </summary>
        /// <param name="event">The event to publish.</param>
        /// <param name="options">
        /// Per-delivery overrides; pass <c>null</c> to use the channel defaults.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task PublishAsync(CloudEvent @event, TOptions? options, CancellationToken cancellationToken = default);
    }
}
