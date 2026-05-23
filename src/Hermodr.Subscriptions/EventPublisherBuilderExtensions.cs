//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Filters;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr
{
    /// <summary>
    /// Extension methods for <see cref="EventPublisherBuilder"/> that add dispatcher and
    /// subscription support to the publisher pipeline.
    /// </summary>
    public static class EventPublisherBuilderExtensions
    {
        // ──────────────────────────────────────────────────────────────────────────────
        // Dispatcher registration
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds the in-process event dispatcher services and wires the
        /// <see cref="EventDispatcher"/> into the publisher's middleware pipeline,
        /// returning an <see cref="EventSubscriptionsBuilder"/> for fluent subscription
        /// configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This registers:
        /// <list type="bullet">
        ///   <item>
        ///     <see cref="EventSubscriptionRegistry"/> (singleton) — the default in-memory store,
        ///     exposed as both <see cref="IEventSubscriptionRegistry"/> and
        ///     <see cref="IEventSubscriptionResolver"/>.
        ///   </item>
        ///   <item>
        ///     <see cref="EventDispatcher"/> as a middleware step in the publish pipeline
        ///     (order relative to other <c>Use&lt;T&gt;</c> calls is registration order).
        ///   </item>
        /// </list>
        /// </para>
        /// <para>
        /// Pass a <paramref name="configure"/> action to customise
        /// <see cref="EventDispatcherOptions"/> (e.g. set
        /// <see cref="EventDispatcherOptions.MaxRoutingDepth"/>) inline without a separate call.
        /// </para>
        /// <para>
        /// To return to the parent <see cref="EventPublisherBuilder"/> after configuring
        /// subscriptions, use the <see cref="EventSubscriptionsBuilder.Builder"/> property or
        /// prefer the <see cref="AddSubscriptions(EventPublisherBuilder, Action{EventSubscriptionsBuilder})"/>
        /// overload.
        /// </para>
        /// </remarks>
        /// <param name="builder">The builder to configure.</param>
        /// <param name="configure">
        /// An optional action to configure <see cref="EventDispatcherOptions"/> for this
        /// publisher's dispatcher (e.g. to set <c>MaxRoutingDepth</c>).
        /// </param>
        /// <returns>
        /// An <see cref="EventSubscriptionsBuilder"/> for fluent subscription registration.
        /// Call <see cref="EventSubscriptionsBuilder.Builder"/> to return to the parent
        /// <see cref="EventPublisherBuilder"/>.
        /// </returns>
        public static EventSubscriptionsBuilder AddSubscriptions(
            this EventPublisherBuilder builder,
            Action<EventDispatcherOptions>? configure = null)
        {
            RegisterDispatcherServices(builder, configure);
            return new EventSubscriptionsBuilder(builder);
        }

        /// <summary>
        /// Adds the in-process event dispatcher services, invokes <paramref name="configure"/>
        /// to register subscriptions via an <see cref="EventSubscriptionsBuilder"/>, and then
        /// returns the parent <see cref="EventPublisherBuilder"/> so that publisher-level
        /// configuration can continue.
        /// </summary>
        /// <param name="builder">The builder to configure.</param>
        /// <param name="configure">
        /// An action that receives an <see cref="EventSubscriptionsBuilder"/> and registers
        /// subscriptions (and optionally configures dispatcher options) inline.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        /// <example>
        /// <code language="csharp">
        /// services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
        ///         .AddSubscriptions(subs =>
        ///         {
        ///             subs.ConfigureOptions(o => o.ThrowOnHandlerError = true);
        ///             subs.Subscribe("com.example.order.*", HandleOrderAsync);
        ///             subs.Subscribe&lt;AuditSubscription&gt;();
        ///         })
        ///         .AddChannel&lt;MyChannel&gt;();
        /// </code>
        /// </example>
        public static EventPublisherBuilder AddSubscriptions(
            this EventPublisherBuilder builder,
            Action<EventSubscriptionsBuilder> configure)
        {
            RegisterDispatcherServices(builder, configure: null);
            var subscriptionsBuilder = new EventSubscriptionsBuilder(builder);
            configure(subscriptionsBuilder);
            return builder;
        }

        private static void RegisterDispatcherServices(
            EventPublisherBuilder builder,
            Action<EventDispatcherOptions>? configure)
        {
            // Register the concrete registry as a singleton so both interface registrations
            // below resolve the same instance.
            builder.Services.TryAddSingleton<EventSubscriptionRegistry>(sp =>
            {
                var subscriptions = sp.GetServices<IEventSubscription>();
                return new EventSubscriptionRegistry(subscriptions);
            });

            // Write interface.
            builder.Services.TryAddSingleton<IEventSubscriptionRegistry>(
                sp => sp.GetRequiredService<EventSubscriptionRegistry>());

            // Read interface — always added (not TryAdd) so that IEnumerable<IEventSubscriptionResolver>
            // picks it up alongside any custom resolvers added via AddSubscriptionResolver<T>.
            builder.Services.AddSingleton<IEventSubscriptionResolver>(
                sp => sp.GetRequiredService<EventSubscriptionRegistry>());

            // Ensure the standard options infrastructure for EventDispatcherOptions is
            // registered so that any configure action (and any later Configure<T>() calls)
            // are respected by the EventDispatcher constructor.
            builder.Services.AddOptions<EventDispatcherOptions>();

            if (configure is not null)
                builder.Services.Configure(configure);

            // Wire the EventDispatcher into the pipeline at registration time — no runtime
            // UseDispatcher() call is needed.
            builder.Use<EventDispatcher>();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Custom resolver registration (EventPublisherBuilder convenience overload)
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
        /// <param name="builder">The builder to configure.</param>
        /// <param name="lifetime">
        /// The service lifetime for <typeparamref name="TResolver"/>.
        /// Defaults to <see cref="ServiceLifetime.Singleton"/>.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        /// <remarks>
        /// This is the right extension point for read-only sources such as a remote subscription
        /// service, a read-only database view, or a static configuration file.  Resolvers are
        /// queried in registration order; matches from all resolvers are aggregated before
        /// dispatching begins.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// builder.AddSubscriptions()
        ///        .AddSubscriptionResolver&lt;RemoteSubscriptionResolver&gt;();
        /// </code>
        /// </example>
        public static EventPublisherBuilder AddSubscriptionResolver<TResolver>(
            this EventPublisherBuilder builder,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TResolver : class, IEventSubscriptionResolver
        {
            // Register the concrete type so it can be resolved independently (e.g. for testing).
            builder.Services.TryAdd(new ServiceDescriptor(typeof(TResolver), typeof(TResolver), lifetime));

            // Expose it as IEventSubscriptionResolver so the dispatcher picks it up via
            // IEnumerable<IEventSubscriptionResolver>.
            builder.Services.Add(new ServiceDescriptor(
                typeof(IEventSubscriptionResolver),
                sp => sp.GetRequiredService<TResolver>(),
                lifetime));

            return builder;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Inline (delegate-based) subscriptions (EventPublisherBuilder convenience overloads)
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a subscription using a pre-built <paramref name="filter"/> and a
        /// <paramref name="handler"/> delegate.
        /// </summary>
        public static EventPublisherBuilder Subscribe(
            this EventPublisherBuilder builder,
            FilterExpression filter,
            Func<CloudEvent, CancellationToken, Task> handler,
            string? name = null)
        {
            builder.Services.AddSingleton<IEventSubscription>(
                new EventSubscription(filter, handler, name));
            return builder;
        }

        /// <summary>
        /// Registers a subscription for events whose <c>type</c> matches <paramref name="typePattern"/>.
        /// </summary>
        /// <param name="builder">The builder to configure.</param>
        /// <param name="typePattern">
        /// An event type pattern.  A trailing <c>*</c> enables prefix matching;
        /// a leading <c>*</c> enables suffix matching; otherwise an exact match is used.
        /// </param>
        /// <param name="handler">The handler invoked when an event matches.</param>
        /// <param name="name">An optional human-readable name for the subscription.</param>
        public static EventPublisherBuilder Subscribe(
            this EventPublisherBuilder builder,
            string typePattern,
            Func<CloudEvent, CancellationToken, Task> handler,
            string? name = null)
        {
            var filter = EventFilter.ByTypePattern(typePattern);
            return builder.Subscribe(filter, handler, name);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Class-based IEventSubscription registration (EventPublisherBuilder convenience overload)
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
        /// <param name="builder">The builder to configure.</param>
        /// <param name="lifetime">
        /// The service lifetime used when registering <typeparamref name="TSubscription"/>.
        /// Defaults to <see cref="ServiceLifetime.Singleton"/>.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        /// <example>
        /// <code language="csharp">
        /// public sealed class AuditOrderSubscription : IEventSubscription
        /// {
        ///     private readonly IAuditService _audit;
        ///     public AuditOrderSubscription(IAuditService audit) => _audit = audit;
        ///
        ///     public string? Name => "audit-orders";
        ///     public FilterExpression Filter =>
        ///         EventFilter.ByTypePattern("com.example.order.*");
        ///
        ///     public Task HandleAsync(CloudEvent e, CancellationToken ct = default)
        ///         => _audit.RecordAsync(e, ct);
        /// }
        ///
        /// // Registration:
        /// builder.AddSubscriptions()
        ///        .Subscribe&lt;AuditOrderSubscription&gt;();
        /// </code>
        /// </example>
        public static EventPublisherBuilder Subscribe<TSubscription>(
            this EventPublisherBuilder builder,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TSubscription : class, IEventSubscription
        {
            builder.Services.TryAdd(new ServiceDescriptor(typeof(TSubscription), typeof(TSubscription), lifetime));

            // Expose the concrete type as IEventSubscription so the registry picks it up.
            builder.Services.Add(new ServiceDescriptor(
                typeof(IEventSubscription),
                sp => sp.GetRequiredService<TSubscription>(),
                lifetime));

            return builder;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Routing subscriptions (EventPublisherBuilder convenience overloads)
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a <see cref="RoutingEventSubscription"/> that, when an event matches
        /// <paramref name="filter"/>, re-publishes it through the <see cref="EventPublisher"/>
        /// pipeline using the specified <paramref name="routingOptions"/> to select the target
        /// channel.
        /// </summary>
        /// <param name="builder">The builder to configure.</param>
        /// <param name="filter">
        /// The filter criteria that determines which events are routed.
        /// </param>
        /// <param name="routingOptions">
        /// The <see cref="EventPublishOptions"/> forwarded to
        /// <see cref="EventPublisher.PublishEventAsync(CloudNative.CloudEvents.CloudEvent, EventPublishOptions, System.Threading.CancellationToken)"/> to select the target channel.
        /// When <c>null</c> the publisher uses its default channel-selection rules.
        /// </param>
        /// <param name="name">An optional human-readable name for this subscription.</param>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        public static EventPublisherBuilder RouteToChannel(
            this EventPublisherBuilder builder,
            FilterExpression filter,
            EventPublishOptions? routingOptions = null,
            string? name = null)
        {
            builder.Services.AddSingleton<IEventSubscription>(sp =>
                new RoutingEventSubscription(filter, sp, routingOptions, name));
            return builder;
        }

        /// <summary>
        /// Registers a <see cref="RoutingEventSubscription"/> that, when an event whose
        /// <c>type</c> matches <paramref name="typePattern"/> is received, re-publishes it
        /// through the <see cref="EventPublisher"/> pipeline using the specified
        /// <paramref name="routingOptions"/> to select the target channel.
        /// </summary>
        /// <param name="builder">The builder to configure.</param>
        /// <param name="typePattern">
        /// An event type pattern.  A trailing <c>*</c> enables prefix matching;
        /// a leading <c>*</c> enables suffix matching; otherwise an exact match is used.
        /// </param>
        /// <param name="routingOptions">
        /// The <see cref="EventPublishOptions"/> forwarded to
        /// <see cref="EventPublisher.PublishEventAsync(CloudNative.CloudEvents.CloudEvent, EventPublishOptions, System.Threading.CancellationToken)"/> to select the target channel.
        /// When <c>null</c> the publisher uses its default channel-selection rules.
        /// </param>
        /// <param name="name">An optional human-readable name for this subscription.</param>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        public static EventPublisherBuilder RouteToChannel(
            this EventPublisherBuilder builder,
            string typePattern,
            EventPublishOptions? routingOptions = null,
            string? name = null)
        {
            var filter = EventFilter.ByTypePattern(typePattern);
            return builder.RouteToChannel(filter, routingOptions, name);
        }
    }
}

