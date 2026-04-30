//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Thread-safe, in-memory implementation of <see cref="IEventSubscriptionRegistry"/>.
    /// </summary>
    public sealed class EventSubscriptionRegistry : IEventSubscriptionRegistry
    {
        private readonly List<IEventSubscription> _subscriptions = new();
        private readonly object _lock = new();

        /// <summary>
        /// Initialises the registry pre-populated with the given <paramref name="subscriptions"/>.
        /// </summary>
        public EventSubscriptionRegistry(IEnumerable<IEventSubscription>? subscriptions = null)
        {
            if (subscriptions is not null)
                _subscriptions.AddRange(subscriptions);
        }
        
        /// <summary>
        /// Registers a new <paramref name="subscription"/> in the registry.
        /// </summary>
        /// <param name="subscription">The subscription to register.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="subscription"/> is <c>null</c>.</exception>
        public void Register(IEventSubscription subscription)
        {
            ArgumentNullException.ThrowIfNull(subscription);
            lock (_lock)
                _subscriptions.Add(subscription);
        }

        /// <inheritdoc/>
        public Task RegisterAsync(IEventSubscription subscription, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(subscription);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
                _subscriptions.Add(subscription);
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Returns all subscriptions whose <see cref="IEventSubscription.Filter"/> matches
        /// the given <paramref name="event"/>.
        /// </summary>
        /// <param name="event">The event to match against.</param>
        /// <returns>A read-only list of matching subscriptions.</returns>
        public IReadOnlyList<IEventSubscription> GetMatchingSubscriptions(CloudEvent @event)
        {
            ArgumentNullException.ThrowIfNull(@event);
            return GetMatchingSnapshot(@event, EventSubscriptionContext.Empty);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
            CloudEvent @event,
            CancellationToken cancellationToken = default)
            => ResolveSubscriptionsAsync(@event, context: null, cancellationToken);

        /// <inheritdoc/>
        public Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
            CloudEvent @event,
            EventSubscriptionContext? context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<IEventSubscription> result =
                GetMatchingSnapshot(@event, context ?? EventSubscriptionContext.Empty);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Takes a thread-safe snapshot of the subscription list and filters it against
        /// the given <paramref name="event"/> and <paramref name="context"/>.
        /// </summary>
        private List<IEventSubscription> GetMatchingSnapshot(CloudEvent @event, EventSubscriptionContext context)
        {
            List<IEventSubscription> snapshot;
            lock (_lock)
                snapshot = new List<IEventSubscription>(_subscriptions);

            return snapshot
                .Where(s => s.Filter.Matches(@event, context))
                .ToList();
        }
    }
}
