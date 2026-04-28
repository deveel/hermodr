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
        
        public IReadOnlyList<IEventSubscription> GetMatchingSubscriptions(CloudEvent @event)
        {
            ArgumentNullException.ThrowIfNull(@event);

            List<IEventSubscription> snapshot;
            lock (_lock)
                snapshot = new List<IEventSubscription>(_subscriptions);

            return snapshot
                .Where(s => s.Filter.Matches(@event, EventSubscriptionContext.Empty))
                .ToList();
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

            List<IEventSubscription> snapshot;
            lock (_lock)
                snapshot = new List<IEventSubscription>(_subscriptions);

            IReadOnlyList<IEventSubscription> result = snapshot
                .Where(s => s.Filter.Matches(@event, context ?? EventSubscriptionContext.Empty))
                .ToList();

            return Task.FromResult(result);
        }
    }
}
