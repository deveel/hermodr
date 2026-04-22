//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// An <see cref="IEventPublishChannel{TOptions}"/> that also supports delivering
    /// multiple events in a single message (batch delivery).
    /// </summary>
    /// <typeparam name="TOptions">
    /// The per-delivery options type accepted by this channel.
    /// </typeparam>
    public interface IBatchEventPublishChannel<TOptions> : IEventPublishChannel<TOptions>
        where TOptions : class
    {
        /// <summary>
        /// Publishes a batch of events using the channel-level defaults.
        /// </summary>
        /// <param name="events">The events to deliver. Must not be empty.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task PublishBatchAsync(
            IReadOnlyList<CloudEvent> events,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes a batch of events, applying <paramref name="options"/> on top
        /// of the channel-level defaults for this delivery only.
        /// </summary>
        /// <param name="events">The events to deliver. Must not be empty.</param>
        /// <param name="options">Per-delivery overrides; <c>null</c> to use defaults.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task PublishBatchAsync(
            IReadOnlyList<CloudEvent> events,
            TOptions? options,
            CancellationToken cancellationToken = default);
    }
}
