//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events {
    /// <summary>
    /// An <see cref="IEventPublishChannel{TEvent}"/> implementation used in tests
    /// that forwards received events to an <see cref="IEventPublishCallback"/>.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The event data class this channel is keyed against.
    /// </typeparam>
    class TypedTestEventPublishChannel<TEvent> : IEventPublishChannel<TEvent>, INamedEventPublishChannel
        where TEvent : class
    {
        private readonly IEventPublishCallback _callback;
        private readonly string? _name;

        public TypedTestEventPublishChannel(IEventPublishCallback callback, string? name = null) {
            _callback = callback;
            _name = name;
        }

        /// <inheritdoc/>
        string? INamedEventPublishChannel.Name => _name;

        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default) {
            return _callback.OnEventPublishedAsync(@event);
        }
    }
}
