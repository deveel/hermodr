// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Wraps an <see cref="IEventPublishChannel"/> and exposes a logical name via
    /// <see cref="INamedEventPublishChannel"/>, without requiring the underlying
    /// channel implementation to carry that concern.
    /// </summary>
    internal sealed class NamedChannelDecorator : IEventPublishChannelDecorator, INamedEventPublishChannel
    {
        private readonly IEventPublishChannel _inner;

        public NamedChannelDecorator(IEventPublishChannel inner, string name)
        {
            _inner = inner;
            Name = name;
        }

        /// <inheritdoc/>
        public IEventPublishChannel InnerChannel => _inner;

        /// <inheritdoc/>
        public string? Name { get; }

        /// <inheritdoc/>
        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            => _inner.PublishAsync(@event, options, cancellationToken);
    }

    /// <summary>
    /// Wraps a typed <see cref="IEventPublishChannel{TEvent}"/> and exposes a logical
    /// name via <see cref="INamedEventPublishChannel"/>, without requiring the underlying
    /// channel to carry the naming concern.
    /// </summary>
    /// <typeparam name="TEvent">The event data class this channel is keyed against.</typeparam>
    internal sealed class NamedChannelDecorator<TEvent> : IEventPublishChannelDecorator, INamedEventPublishChannel, IEventPublishChannel<TEvent>
        where TEvent : class
    {
        private readonly IEventPublishChannel<TEvent> _inner;

        public NamedChannelDecorator(IEventPublishChannel<TEvent> inner, string name)
        {
            _inner = inner;
            Name = name;
        }

        /// <inheritdoc/>
        public IEventPublishChannel InnerChannel => _inner;

        /// <inheritdoc/>
        public string? Name { get; }

        /// <inheritdoc/>
        public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            => _inner.PublishAsync(@event, options, cancellationToken);
    }
}



