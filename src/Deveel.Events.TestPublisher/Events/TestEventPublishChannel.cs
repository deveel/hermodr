//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events {
    /// <summary>
    /// An in-memory <see cref="IEventPublishChannel"/> that forwards every received
    /// <see cref="CloudNative.CloudEvents.CloudEvent"/> to an
    /// <see cref="IEventPublishCallback"/> instead of a real transport.
    /// </summary>
    class TestEventPublishChannel : IEventPublishChannel, INamedEventPublishChannel {
        private readonly IEventPublishCallback _callback;
        private readonly string? _name;

        /// <summary>
        /// Constructs the channel with the callback to invoke when an event is published.
        /// </summary>
        /// <param name="callback">The callback to invoke on publish.</param>
        /// <param name="name">An optional logical name for this channel instance.</param>
        public TestEventPublishChannel(IEventPublishCallback callback, string? name = null) {
            _callback = callback;
            _name = name;
        }

        /// <inheritdoc/>
        string? INamedEventPublishChannel.Name => _name;

        /// <inheritdoc/>
        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default) {
            return _callback.OnEventPublishedAsync(@event);
        }
    }
}
