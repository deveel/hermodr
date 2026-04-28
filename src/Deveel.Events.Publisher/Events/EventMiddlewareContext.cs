//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Carries state through the event-publish middleware pipeline for a single
    /// <see cref="EventPublisher.PublishEventAsync(CloudNative.CloudEvents.CloudEvent, EventPublishOptions, System.Threading.CancellationToken)"/> or
    /// <see cref="EventPublisher.PublishAsync(System.Type, object, EventPublishOptions, System.Threading.CancellationToken)"/> call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Middleware components receive an <see cref="EventMiddlewareContext"/> and may
    /// inspect or mutate it before invoking the <c>next</c> delegate.  Common patterns:
    /// <list type="bullet">
    ///   <item><description>Enrich the event with a correlation ID (<see cref="Event"/>).</description></item>
    ///   <item><description>Resolve scoped services via <see cref="Services"/> (e.g. a
    ///     correlation-ID accessor or a schema validator).</description></item>
    ///   <item><description>Override per-call options (<see cref="Options"/>).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The context is created fresh for each inbound publish call and is
    /// <strong>not</strong> shared across concurrent calls.
    /// </para>
    /// </remarks>
    public sealed class EventMiddlewareContext
    {
        /// <summary>
        /// Initialises a new <see cref="EventMiddlewareContext"/>.
        /// </summary>
        /// <param name="event">The enriched <see cref="CloudEvent"/> to publish.</param>
        /// <param name="services">
        /// The <see cref="IServiceProvider"/> that middleware uses to resolve its
        /// runtime dependencies (e.g. loggers, validators, correlation-ID accessors).
        /// </param>
        /// <param name="cancellationToken">
        /// A token that signals cancellation of the entire publish operation.
        /// Middleware should forward this token when starting its own async work.
        /// </param>
        /// <param name="options">
        /// Optional per-call options that override the channel-level defaults.
        /// </param>
        public EventMiddlewareContext(
            CloudEvent @event,
            IServiceProvider services,
            CancellationToken cancellationToken = default,
            EventPublishOptions? options = null)
        {
            Event = @event ?? throw new ArgumentNullException(nameof(@event));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            CancellationToken = cancellationToken;
            Options = options;
        }

        /// <summary>
        /// Gets or sets the <see cref="CloudEvent"/> being published.
        /// </summary>
        /// <remarks>
        /// Middleware may replace this with an enriched copy of the event (e.g. to
        /// inject a correlation-ID extension or override the <c>source</c> attribute).
        /// </remarks>
        public CloudEvent Event { get; set; }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> associated with the current
        /// publish operation.
        /// </summary>
        /// <remarks>
        /// Use this to resolve services needed during middleware execution (e.g.
        /// <c>ILogger&lt;T&gt;</c>, a correlation-ID accessor, or a schema registry
        /// client).  The provider is the same one injected into the
        /// <see cref="EventPublisher"/> from the DI container.
        /// </remarks>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Gets the <see cref="System.Threading.CancellationToken"/> for the current
        /// publish operation.
        /// </summary>
        /// <remarks>
        /// Middleware should pass this token to any async work it starts so that the
        /// entire pipeline can be cancelled cooperatively.
        /// </remarks>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Gets or sets the per-call publish options, or <c>null</c> to use each
        /// channel's registered defaults.
        /// </summary>
        public EventPublishOptions? Options { get; set; }
    }
}


