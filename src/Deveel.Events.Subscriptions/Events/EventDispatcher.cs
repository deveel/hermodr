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
    /// Internal middleware that routes published events to matching subscriptions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When enabled in the publish pipeline (via
    /// <see cref="EventPublisherExtensions.UseDispatcher(EventPublisher)"/>), the dispatcher receives
    /// every published <see cref="CloudEvent"/> and routes it to the matching
    /// <see cref="IEventSubscription"/> instances by querying <em>all</em> registered
    /// <see cref="IEventSubscriptionResolver"/> instances in order.
    /// </para>
    /// <para>
    /// Multiple <see cref="IEventSubscriptionResolver"/> implementations can be registered with the
    /// DI container (e.g. an in-memory <see cref="IEventSubscriptionRegistry"/> plus a
    /// read-only remote resolver). The dispatcher aggregates matching subscriptions from every
    /// resolver before invoking handlers.
    /// </para>
    /// <para>
    /// Exceptions thrown by individual subscription handlers are caught and logged; by default
    /// they do not propagate so that a single failing subscriber cannot prevent other subscribers
    /// from receiving the event. Set <see cref="EventDispatcherOptions.ThrowOnHandlerError"/> to
    /// <c>true</c> to change this behaviour.
    /// </para>
    /// </remarks>
    internal class EventDispatcher : IEventMiddleware
    {
        private readonly IReadOnlyList<IEventSubscriptionResolver> _resolvers;
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
        /// <param name="options">Optional dispatcher-level options.</param>
        /// <param name="logger">Optional logger.</param>
        [ActivatorUtilitiesConstructor]
        public EventDispatcher(
            IEnumerable<IEventSubscriptionResolver> resolvers,
            EventDispatcherOptions? options = null,
            ILogger<EventDispatcher>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(resolvers);
            _resolvers = resolvers.ToList();
            _options = options ?? new EventDispatcherOptions();
            _logger = logger ?? NullLogger<EventDispatcher>.Instance;
        }


        private async Task DispatchWithServicesAsync(
            CloudEvent @event,
            IServiceProvider? services,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var context = services is not null ? new EventSubscriptionContext(services) : null;

            var subscriptions = new List<IEventSubscription>();
            var resolveTasks = _resolvers.Select(resolver =>
                resolver.ResolveSubscriptionsAsync(@event, context, cancellationToken));

            foreach (var resolveTask in resolveTasks)
            {
                var resolved = await resolveTask;
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
        public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            await DispatchWithServicesAsync(context.Event, context.Services, context.CancellationToken);
            await next(context);
        }
    }
}

