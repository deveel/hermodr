//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr {
    /// <summary>
    /// An in-memory <see cref="IEventPublishChannel"/> that forwards every received
    /// <see cref="CloudNative.CloudEvents.CloudEvent"/> to an
    /// <see cref="IEventPublishCallback"/> instead of a real transport.
    /// </summary>
    class TestEventPublishChannel : IEventPublishChannel {
        private readonly IEventPublishCallback _callback;

        /// <summary>
        /// Constructs the channel with the callback to invoke when an event is published.
        /// </summary>
        /// <param name="callback">The callback to invoke on publish.</param>
        public TestEventPublishChannel(IEventPublishCallback callback) {
            _callback = callback;
        }

        /// <inheritdoc/>
        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default) {
            return _callback.OnEventPublishedAsync(@event);
        }
    }
}
