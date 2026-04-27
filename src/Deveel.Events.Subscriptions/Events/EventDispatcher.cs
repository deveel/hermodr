//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deveel.Events
{
    /// <summary>
    /// Default implementation of <see cref="IEventDispatcher"/> that also participates in the
    /// publish pipeline as an <see cref="IEventPublishChannel"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When registered as an <see cref="IEventPublishChannel"/> (via
    /// <see cref="EventPublisherBuilderExtensions.AddDispatcher"/>), the dispatcher receives
    /// every published <see cref="CloudEvent"/> and routes it to the matching
    /// <see cref="IEventSubscription"/> instances by querying <em>all</em> registered
    /// <see cref="IEventSubscriptionResolver"/> instances in order.
    /// </para>
    /// <para>
    /// Multiple <see cref="IEventSubscriptionResolver"/> implementations can be registered with the
    /// DI container (e.g. an in-memory <see cref="IEventSubscriptionRegistry"/> plus a
    /// read-only remote resolver).  The dispatcher aggregates matching subscriptions from every
    /// resolver before invoking handlers.
    /// </para>
    /// <para>
    /// Exceptions thrown by individual subscription handlers are caught and logged; by default
    /// they do not propagate so that a single failing subscriber cannot prevent other subscribers
    /// from receiving the event.  Set <see cref="EventDispatcherOptions.ThrowOnHandlerError"/> to
    /// <c>true</c> to change this behaviour.
    /// </para>
    /// </remarks>
    public class EventDispatcher : IEventDispatcher, IEventPublishChannel
    {
        private readonly IReadOnlyList<IEventSubscriptionResolver> _resolvers;
        private readonly IServiceProvider? _services;
        private readonly EventDispatcherOptions _options;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new <see cref="EventDispatcher"/> backed by multiple resolvers.
        /// This constructor is used by the DI container.
        /// </summary>
        /// <param name="resolvers">
        /// All <see cref="IEventSubscriptionResolver"/> instances registered with the DI container.
        /// Matching subscriptions are aggregated from every resolver in order.
        /// </param>
        /// <param name="services">
        /// The application <see cref="IServiceProvider"/>, forwarded to DI-aware filters so that
        /// runtime-registered services can be resolved when evaluating subscriptions.
        /// May be <c>null</c> when no DI container is available.
        /// </param>
        /// <param name="options">
        /// Optional dispatcher-level options.  When <c>null</c> defaults are used.
        /// </param>
        /// <param name="logger">
        /// Optional logger; when <c>null</c> a <see cref="NullLogger"/> is used.
        /// </param>
        [ActivatorUtilitiesConstructor]
        public EventDispatcher(
            IEnumerable<IEventSubscriptionResolver> resolvers,
            IServiceProvider? services = null,
            EventDispatcherOptions? options = null,
            ILogger<EventDispatcher>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(resolvers);
            _resolvers = resolvers.ToList();
            _services = services;
            _options = options ?? new EventDispatcherOptions();
            _logger = logger ?? NullLogger<EventDispatcher>.Instance;
        }

        /// <summary>
        /// Creates a new <see cref="EventDispatcher"/> backed by a single resolver.
        /// Useful for direct instantiation in tests and simple scenarios.
        /// </summary>
        /// <param name="resolver">The resolver to query when dispatching events.</param>
        /// <param name="services">Optional service provider forwarded to data filters.</param>
        /// <param name="options">Optional dispatcher-level options.</param>
        /// <param name="logger">Optional logger.</param>
        public EventDispatcher(
            IEventSubscriptionResolver resolver,
            IServiceProvider? services = null,
            EventDispatcherOptions? options = null,
            ILogger<EventDispatcher>? logger = null)
            : this([resolver ?? throw new ArgumentNullException(nameof(resolver))],
                   services, options, logger)
        {
        }

        /// <inheritdoc/>
        public async Task DispatchAsync(CloudEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            // Build context once and share it across all resolver calls.
            var context = _services is not null
                ? new EventSubscriptionContext(_services)
                : null;

            // Aggregate matching subscriptions from every registered resolver.
            var subscriptions = new List<IEventSubscription>();
            foreach (var resolver in _resolvers)
            {
                var resolved = await resolver.ResolveSubscriptionsAsync(
                    @event, context, cancellationToken);
                subscriptions.AddRange(resolved);
            }

            if (subscriptions.Count == 0)
            {
                _logger.LogNoMatchingSubscriptions(@event.Type);
                return;
            }

            foreach (var subscription in subscriptions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name = subscription.Name ?? "<anonymous>";

                _logger.LogDispatching(@event.Type, name);

                try
                {
                    await subscription.HandleAsync(@event, cancellationToken);

                    _logger.LogDispatched(@event.Type, name);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogSubscriptionHandlerError(ex, name, @event.Type);

                    if (_options.ThrowOnHandlerError)
                        throw;
                }
            }
        }

        /// <inheritdoc/>
        Task IEventPublishChannel.PublishAsync(
            CloudEvent @event,
            EventPublishOptions? options,
            CancellationToken cancellationToken)
            => DispatchAsync(@event, cancellationToken);
    }
}

