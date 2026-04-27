//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// An implementation of <see cref="IRoutingEventSubscription"/> that re-publishes a
    /// matched <see cref="CloudEvent"/> through the <see cref="IEventPublisher"/> pipeline,
    /// optionally targeting a specific channel via <see cref="RoutingOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="IEventPublisher"/> is resolved lazily from the <see cref="IServiceProvider"/>
    /// on the first call to <see cref="HandleAsync"/> rather than at construction time.
    /// This breaks the potential circular dependency that would occur if
    /// <see cref="IEventPublisher"/> were injected directly: the publisher pipeline depends
    /// on <see cref="EventDispatcher"/> (as an <see cref="IEventPublishChannel"/>), which
    /// in turn depends on <see cref="IEventSubscriptionRegistry"/>, which enumerates all
    /// registered <see cref="IEventSubscription"/> instances — including this one.
    /// </para>
    /// <para>
    /// The registered <see cref="IEventPublisher"/> is itself a singleton; resolving it
    /// lazily at call time is therefore safe and incurs no meaningful overhead.
    /// </para>
    /// </remarks>
    public sealed class RoutingEventSubscription : IRoutingEventSubscription
    {
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initialises a new routing subscription.
        /// </summary>
        /// <param name="filter">
        /// The filter that determines which events trigger the routing.
        /// </param>
        /// <param name="services">
        /// The application <see cref="IServiceProvider"/> used to lazily resolve the
        /// <see cref="IEventPublisher"/> at handle time.
        /// </param>
        /// <param name="routingOptions">
        /// The <see cref="EventPublishOptions"/> forwarded to
        /// <see cref="IEventPublisher.PublishEventAsync"/> to select the target channel.
        /// When <c>null</c> the publisher uses its default channel-selection rules.
        /// </param>
        /// <param name="name">An optional human-readable name for this subscription.</param>
        public RoutingEventSubscription(
            IEventFilter filter,
            IServiceProvider services,
            EventPublishOptions? routingOptions = null,
            string? name = null)
        {
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            RoutingOptions = routingOptions;
            Name = name;
        }

        /// <inheritdoc/>
        public string? Name { get; }

        /// <inheritdoc/>
        public IEventFilter Filter { get; }

        /// <inheritdoc/>
        public EventPublishOptions? RoutingOptions { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Resolves the <see cref="IEventPublisher"/> from the service provider and calls
        /// <see cref="IEventPublisher.PublishEventAsync"/> with <see cref="RoutingOptions"/>,
        /// effectively forwarding the matched event through the full publishing pipeline.
        /// </remarks>
        public Task HandleAsync(CloudEvent @event, CancellationToken cancellationToken = default)
        {
            var publisher = _services.GetRequiredService<IEventPublisher>();
            return publisher.PublishEventAsync(@event, RoutingOptions, cancellationToken);
        }
    }
}

