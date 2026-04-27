//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Carries contextual information that is passed to an
    /// <see cref="IEventSubscriptionResolver"/> when resolving subscriptions for a
    /// dispatched event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The context is intentionally extensible: additional metadata and attributes can be
    /// added here in future without breaking the <see cref="IEventSubscriptionResolver"/>
    /// contract.
    /// </para>
    /// <para>
    /// Instances are created by infrastructure components (e.g. <see cref="EventDispatcher"/>)
    /// and forwarded via
    /// <see cref="IEventSubscriptionResolver.ResolveSubscriptionsAsync(CloudNative.CloudEvents.CloudEvent, EventSubscriptionContext?, System.Threading.CancellationToken)"/>.
    /// Use <see cref="EventSubscriptionContext.Empty"/> when no services or additional data
    /// are available.
    /// </para>
    /// </remarks>
    public sealed class EventSubscriptionContext
    {
        /// <summary>
        /// Initialises a new context with the supplied <paramref name="services"/>.
        /// </summary>
        /// <param name="services">
        /// The application <see cref="IServiceProvider"/> used by DI-aware filters (e.g.
        /// <see cref="TypedDataFilter{T}"/>) to resolve runtime services such as
        /// <see cref="EventDataDeserializerProvider"/>.
        /// May be <c>null</c> when no DI container is available.
        /// </param>
        internal EventSubscriptionContext(IServiceProvider? services)
        {
            Services = services;
        }

        /// <summary>
        /// Gets a shared empty context with no service provider and no additional metadata.
        /// </summary>
        public static EventSubscriptionContext Empty { get; } = new(services: null);

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> that can be used by DI-aware filters to
        /// resolve runtime services.  <c>null</c> when no DI container is associated with
        /// this context.
        /// </summary>
        public IServiceProvider? Services { get; }
    }
}

