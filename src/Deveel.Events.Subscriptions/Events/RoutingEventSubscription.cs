//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Filters;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// An implementation of <see cref="IRoutingEventSubscription"/> that re-publishes a
    /// matched <see cref="CloudEvent"/> through the <see cref="EventPublisher"/> pipeline,
    /// optionally targeting a specific channel via <see cref="RoutingOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="EventPublisher"/> is resolved lazily from the <see cref="IServiceProvider"/>
    /// on the first call to <see cref="HandleAsync"/> rather than at construction time.
    /// This breaks the potential circular dependency that would occur if
    /// <see cref="EventPublisher"/> were injected directly: the publisher pipeline depends
    /// on <see cref="EventDispatcher"/> middleware, which
    /// in turn depends on <see cref="IEventSubscriptionRegistry"/>, which enumerates all
    /// registered <see cref="IEventSubscription"/> instances — including this one.
    /// </para>
    /// <para>
    /// The registered <see cref="EventPublisher"/> is itself a singleton; resolving it
    /// lazily at call time is therefore safe and incurs no meaningful overhead.
    /// </para>
    /// </remarks>
    public sealed class RoutingEventSubscription : IRoutingEventSubscription
    {
        /// <summary>
        /// The maximum number of times a single event may be re-routed through
        /// <see cref="RoutingEventSubscription"/> instances before a loop is detected
        /// and the routing is aborted.
        /// </summary>
        private const int MaxRoutingDepth = 10;

        /// <summary>
        /// The name of the CloudEvents extension attribute used to track the current
        /// routing depth and detect infinite routing loops.
        /// </summary>
        private static readonly CloudEventAttribute RoutingDepthAttribute =
            CloudEventAttribute.CreateExtension("routingdepth", CloudEventAttributeType.Integer);

        private readonly IServiceProvider _services;

        /// <summary>
        /// Initialises a new routing subscription.
        /// </summary>
        /// <param name="filter">
        /// The filter that determines which events trigger the routing.
        /// </param>
        /// <param name="services">
        /// The application <see cref="IServiceProvider"/> used to lazily resolve the
        /// <see cref="EventPublisher"/> at handle time.
        /// </param>
        /// <param name="routingOptions">
        /// The <see cref="EventPublishOptions"/> forwarded to
        /// <see cref="EventPublisher.PublishEventAsync(CloudNative.CloudEvents.CloudEvent,EventPublishOptions,System.Threading.CancellationToken)"/> to select the target channel.
        /// When <c>null</c> the publisher uses its default channel-selection rules.
        /// </param>
        /// <param name="name">An optional human-readable name for this subscription.</param>
        public RoutingEventSubscription(
            FilterExpression filter,
            IServiceProvider services,
            EventPublishOptions? routingOptions = null,
            string? name = null)
        {
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            RoutingOptions = routingOptions;
            Name = name;
        }

        /// <inheritdoc/>
        public string? Name { get; }

        /// <inheritdoc/>
        public FilterExpression Filter { get; }

        /// <inheritdoc/>
        public EventPublishOptions? RoutingOptions { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Resolves the <see cref="EventPublisher"/> from the service provider and calls
        /// <c>PublishEventAsync</c> with <see cref="RoutingOptions"/>,
        /// effectively forwarding the matched event through the full publishing pipeline.
        /// <para>
        /// A routing-depth counter is tracked via the <c>routingdepth</c> CloudEvents
        /// extension attribute. If the depth exceeds <see cref="MaxRoutingDepth"/> the
        /// re-publish is skipped to prevent infinite routing loops.
        /// </para>
        /// </remarks>
        public Task HandleAsync(CloudEvent @event, CancellationToken cancellationToken = default)
        {
            // Loop-detection: increment the routing depth counter; abort if limit exceeded.
            var currentDepth = @event[RoutingDepthAttribute] is int d ? d : 0;
            if (currentDepth >= MaxRoutingDepth)
                return Task.CompletedTask;

            @event[RoutingDepthAttribute] = currentDepth + 1;

            var publisher = _services.GetRequiredService<IEventPublisher>();
            return publisher.PublishEventAsync(@event, RoutingOptions, cancellationToken);
        }
    }
}
