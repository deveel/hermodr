//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    /// <summary>
    /// Internal middleware that routes published events to matching subscriptions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When enabled in the publish pipeline (via
    /// <see cref="EventPublisherBuilderExtensions.AddSubscriptions"/>), the dispatcher receives
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
        // Tracks how deep into a routing chain the current async execution context is.
        // AsyncLocal is used so that each top-level publish call starts at 0 and
        // recursive re-publishes (from RoutingEventSubscription) increment their own
        // slot without affecting sibling or parent publish calls.
        private static readonly AsyncLocal<int> _routingDepth = new();

        private readonly IReadOnlyList<IEventSubscriptionResolver> _resolvers;
        private readonly EventDispatcherOptions _options;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new <see cref="EventDispatcher"/> backed by multiple resolvers.
        /// Constructor dependencies are resolved from the DI container by
        /// <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities"/>.
        /// </summary>
        public EventDispatcher(
            IEnumerable<IEventSubscriptionResolver> resolvers,
            IOptions<EventDispatcherOptions> options,
            ILogger<EventDispatcher>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(resolvers);
            _resolvers = resolvers.ToList();
            _options = options?.Value ?? new EventDispatcherOptions();
            _logger = logger ?? NullLogger<EventDispatcher>.Instance;
        }

        private async Task DispatchWithServicesAsync(
            CloudEvent @event,
            IServiceProvider? services,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var context = services is not null ? new EventSubscriptionContext(services) : null;

            // Fan-out resolver queries in parallel for lower latency.
            var resolveTasks = _resolvers
                .Select(r => r.ResolveSubscriptionsAsync(@event, context, cancellationToken))
                .ToList();

            var resolved = await Task.WhenAll(resolveTasks);
            var subscriptions = resolved.SelectMany(s => s).ToList();

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

            // Routing-loop guard: AsyncLocal tracks depth across recursive publish
            // calls within the same async execution context (e.g. RoutingEventSubscription
            // calling publisher.PublishEventAsync again). Each top-level publish call
            // starts at 0; re-entrant calls inherit the incremented value.
            var depth = _routingDepth.Value;
            if (depth >= _options.MaxRoutingDepth)
            {
                _logger.LogWarning(
                    "Routing depth limit ({MaxRoutingDepth}) reached for event type '{EventType}'. Aborting re-dispatch to prevent an infinite loop.",
                    _options.MaxRoutingDepth, context.Event.Type);
                await next(context);
                return;
            }

            _routingDepth.Value = depth + 1;
            try
            {
                await DispatchWithServicesAsync(context.Event, context.Services, context.CancellationToken);
                await next(context);
            }
            finally
            {
                _routingDepth.Value = depth;
            }
        }
    }
}

