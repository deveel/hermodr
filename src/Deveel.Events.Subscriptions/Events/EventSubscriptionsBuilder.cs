//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Filters;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Deveel.Events
{
    /// <summary>
    /// A fluent builder for configuring event subscriptions within the
    /// <see cref="EventPublisher"/> pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Obtain an instance by calling
    /// <see cref="EventPublisherBuilderExtensions.AddSubscriptions(EventPublisherBuilder, Action{EventDispatcherOptions}?)"/>
    /// on an <see cref="EventPublisherBuilder"/>.  All <c>Subscribe</c>,
    /// <see cref="AddSubscriptionResolver{TResolver}"/> and <see cref="RouteToChannel(FilterExpression, EventPublishOptions?, string?)"/>
    /// methods return <c>this</c> so they can be chained fluently.
    /// </para>
    /// <para>
    /// Use <see cref="Builder"/> or call
    /// <see cref="EventPublisherBuilderExtensions.AddSubscriptions(EventPublisherBuilder, Action{EventSubscriptionsBuilder})"/>
    /// to return to the parent <see cref="EventPublisherBuilder"/> chain when further
    /// publisher-level configuration is required.
    /// </para>
    /// </remarks>
    public sealed class EventSubscriptionsBuilder
    {
        internal EventSubscriptionsBuilder(EventPublisherBuilder builder)
        {
            Builder = builder;
        }

        /// <summary>
        /// Gets the parent <see cref="EventPublisherBuilder"/> so that publisher-level
        /// configuration can continue after subscription configuration is complete.
        /// </summary>
        private EventPublisherBuilder Builder { get; }

        // ──────────────────────────────────────────────────────────────────────────────
        // Dispatcher options
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Configures the <see cref="EventDispatcherOptions"/> for the dispatcher that
        /// is wired into the publisher pipeline.
        /// </summary>
        /// <param name="configure">
        /// An action that receives and mutates the <see cref="EventDispatcherOptions"/>.
        /// </param>
        /// <returns>This builder for chaining.</returns>
        public EventSubscriptionsBuilder ConfigureOptions(Action<EventDispatcherOptions> configure)
        {
            Builder.Services.Configure(configure);
            return this;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Custom resolver registration
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a custom <see cref="IEventSubscriptionResolver"/> implementation that the
        /// <see cref="EventDispatcher"/> will query alongside the built-in
        /// <see cref="EventSubscriptionRegistry"/>.
        /// </summary>
        /// <typeparam name="TResolver">
        /// A concrete type implementing <see cref="IEventSubscriptionResolver"/>.  The type must
        /// be constructable by the DI container (constructor injection is supported).
        /// </typeparam>
        /// <param name="lifetime">
        /// The service lifetime for <typeparamref name="TResolver"/>.
        /// Defaults to <see cref="ServiceLifetime.Singleton"/>.
        /// </param>
        /// <returns>This builder for chaining.</returns>
        public EventSubscriptionsBuilder AddSubscriptionResolver<TResolver>(
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TResolver : class, IEventSubscriptionResolver
        {
            Builder.Services.TryAdd(new ServiceDescriptor(typeof(TResolver), typeof(TResolver), lifetime));
            Builder.Services.Add(new ServiceDescriptor(
                typeof(IEventSubscriptionResolver),
                sp => sp.GetRequiredService<TResolver>(),
                lifetime));
            return this;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Inline (delegate-based) subscriptions
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a subscription using a pre-built <paramref name="filter"/> and a
        /// <paramref name="handler"/> delegate.
        /// </summary>
        /// <param name="filter">The filter criteria that determines which events are handled.</param>
        /// <param name="handler">The handler invoked when an event matches.</param>
        /// <param name="name">An optional human-readable name for the subscription.</param>
        /// <returns>This builder for chaining.</returns>
        public EventSubscriptionsBuilder Subscribe(
            FilterExpression filter,
            Func<CloudEvent, CancellationToken, Task> handler,
            string? name = null)
        {
            Builder.Services.AddSingleton<IEventSubscription>(
                new EventSubscription(filter, handler, name));
            return this;
        }

        /// <summary>
        /// Registers a subscription for events whose <c>type</c> matches <paramref name="typePattern"/>.
        /// </summary>
        /// <param name="typePattern">
        /// An event type pattern.  A trailing <c>*</c> enables prefix matching;
        /// a leading <c>*</c> enables suffix matching; otherwise an exact match is used.
        /// </param>
        /// <param name="handler">The handler invoked when an event matches.</param>
        /// <param name="name">An optional human-readable name for the subscription.</param>
        /// <returns>This builder for chaining.</returns>
        public EventSubscriptionsBuilder Subscribe(
            string typePattern,
            Func<CloudEvent, CancellationToken, Task> handler,
            string? name = null)
        {
            var filter = EventFilter.ByTypePattern(typePattern);
            return Subscribe(filter, handler, name);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Class-based IEventSubscription registration
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a user-defined class that directly implements <see cref="IEventSubscription"/>
        /// as a subscription in the dispatcher pipeline.
        /// </summary>
        /// <typeparam name="TSubscription">
        /// A concrete type that implements <see cref="IEventSubscription"/>.  The type is
        /// registered with the DI container using the specified <paramref name="lifetime"/> and
        /// resolved from there when the first event dispatch occurs, so constructor injection
        /// of services is fully supported.
        /// </typeparam>
        /// <param name="lifetime">
        /// The service lifetime used when registering <typeparamref name="TSubscription"/>.
        /// Defaults to <see cref="ServiceLifetime.Singleton"/>.
        /// </param>
        /// <returns>This builder for chaining.</returns>
        public EventSubscriptionsBuilder Subscribe<TSubscription>(
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TSubscription : class, IEventSubscription
        {
            Builder.Services.TryAdd(new ServiceDescriptor(typeof(TSubscription), typeof(TSubscription), lifetime));
            Builder.Services.Add(new ServiceDescriptor(
                typeof(IEventSubscription),
                sp => sp.GetRequiredService<TSubscription>(),
                lifetime));
            return this;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Routing subscriptions
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a <see cref="RoutingEventSubscription"/> that, when an event matches
        /// <paramref name="filter"/>, re-publishes it through the <see cref="EventPublisher"/>
        /// pipeline using the specified <paramref name="routingOptions"/> to select the target channel.
        /// </summary>
        /// <param name="filter">The filter criteria that determines which events are routed.</param>
        /// <param name="routingOptions">
        /// The <see cref="EventPublishOptions"/> forwarded to
        /// <see cref="EventPublisher.PublishEventAsync(CloudNative.CloudEvents.CloudEvent, EventPublishOptions, System.Threading.CancellationToken)"/>
        /// to select the target channel.  When <c>null</c> the publisher uses its default
        /// channel-selection rules.
        /// </param>
        /// <param name="name">An optional human-readable name for this subscription.</param>
        /// <returns>This builder for chaining.</returns>
        public EventSubscriptionsBuilder RouteToChannel(
            FilterExpression filter,
            EventPublishOptions? routingOptions = null,
            string? name = null)
        {
            Builder.Services.AddSingleton<IEventSubscription>(sp =>
                new RoutingEventSubscription(filter, sp, routingOptions, name));
            return this;
        }

        /// <summary>
        /// Registers a <see cref="RoutingEventSubscription"/> that, when an event whose
        /// <c>type</c> matches <paramref name="typePattern"/> is received, re-publishes it
        /// through the <see cref="EventPublisher"/> pipeline using the specified
        /// <paramref name="routingOptions"/> to select the target channel.
        /// </summary>
        /// <param name="typePattern">
        /// An event type pattern.  A trailing <c>*</c> enables prefix matching;
        /// a leading <c>*</c> enables suffix matching; otherwise an exact match is used.
        /// </param>
        /// <param name="routingOptions">
        /// The <see cref="EventPublishOptions"/> forwarded to
        /// <see cref="EventPublisher.PublishEventAsync(CloudNative.CloudEvents.CloudEvent, EventPublishOptions, System.Threading.CancellationToken)"/>
        /// to select the target channel.  When <c>null</c> the publisher uses its default
        /// channel-selection rules.
        /// </param>
        /// <param name="name">An optional human-readable name for this subscription.</param>
        /// <returns>This builder for chaining.</returns>
        public EventSubscriptionsBuilder RouteToChannel(
            string typePattern,
            EventPublishOptions? routingOptions = null,
            string? name = null)
        {
            var filter = EventFilter.ByTypePattern(typePattern);
            return RouteToChannel(filter, routingOptions, name);
        }
    }
}



