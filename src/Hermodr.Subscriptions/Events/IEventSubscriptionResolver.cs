//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// Read-only view over a collection of subscriptions: returns those whose filter
    /// matches a given <see cref="CloudEvent"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separate from <see cref="IEventSubscriptionRegistry"/> so that read-only sources
    /// (remote services, read-only databases, static configuration files, …) can participate
    /// in routing without exposing a write API.
    /// </para>
    /// <para>
    /// Multiple <see cref="IEventSubscriptionResolver"/> implementations can be registered
    /// with the DI container.  <see cref="EventDispatcher"/> iterates all of them and
    /// aggregates the results when dispatching an event.
    /// </para>
    /// </remarks>
    public interface IEventSubscriptionResolver
    {
        /// <summary>
        /// Returns all subscriptions whose <see cref="IEventSubscription.Filter"/>
        /// matches the supplied <paramref name="event"/>.
        /// </summary>
        /// <param name="event">The event to match against registered subscriptions.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
            CloudEvent @event,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all subscriptions whose <see cref="IEventSubscription.Filter"/>
        /// matches the supplied <paramref name="event"/>, using <paramref name="context"/>
        /// to forward runtime metadata (e.g. the application <see cref="IServiceProvider"/>)
        /// to filters that need to resolve DI-registered services at evaluation time.
        /// </summary>
        /// <param name="event">The event to match against registered subscriptions.</param>
        /// <param name="context">
        /// An optional <see cref="EventSubscriptionContext"/> that carries the
        /// <see cref="IServiceProvider"/> and any other metadata needed by DI-aware filters.
        /// Pass <c>null</c> (or <see cref="EventSubscriptionContext.Empty"/>) when no
        /// additional context is available.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <remarks>
        /// The default implementation ignores <paramref name="context"/> and falls back to
        /// <see cref="ResolveSubscriptionsAsync(CloudEvent, CancellationToken)"/>.
        /// Override this method to propagate context through the filter evaluation chain.
        /// </remarks>
        Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
            CloudEvent @event,
            EventSubscriptionContext? context,
            CancellationToken cancellationToken = default)
            => ResolveSubscriptionsAsync(@event, cancellationToken);
    }
}
