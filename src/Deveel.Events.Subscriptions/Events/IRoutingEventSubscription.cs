//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// An <see cref="IEventSubscription"/> that, when an event matches its filter,
    /// re-publishes that event to another channel by calling <see cref="IEventPublisher"/>
    /// with a set of <see cref="EventPublishOptions"/> that select the target channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike a plain <see cref="IEventSubscription"/> whose <c>HandleAsync</c> method
    /// executes arbitrary logic, a routing subscription delegates the actual delivery to
    /// the publisher pipeline (enrichment → validation → fan-out), ensuring that all
    /// pipeline behaviours are applied uniformly to the re-routed event.
    /// </para>
    /// <para>
    /// The concrete implementation is <see cref="RoutingEventSubscription"/>.  Use the
    /// <c>RouteToChannel</c> extension methods on <see cref="EventPublisherBuilder"/> to
    /// register routing subscriptions with the DI container.
    /// </para>
    /// </remarks>
    public interface IRoutingEventSubscription : IEventSubscription
    {
        /// <summary>
        /// Gets the <see cref="EventPublishOptions"/> that describe the target channel
        /// to which the matched event should be routed.
        /// </summary>
        /// <remarks>
        /// Pass a channel-specific options sub-type (e.g. <c>RabbitMqPublishOptions</c>)
        /// to target a single named channel, or a <see cref="CombinedPublishOptions"/>
        /// to fan the event out to multiple channels simultaneously.
        /// When <c>null</c> the publisher uses its default channel selection rules.
        /// </remarks>
        EventPublishOptions? RoutingOptions { get; }
    }
}

