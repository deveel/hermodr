//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// A service that is used to publish events to one or more channels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface is the primary DI injection point for the event publishing
    /// infrastructure.  Application code should always depend on
    /// <see cref="IEventPublisher"/> rather than on the concrete
    /// <see cref="EventPublisher"/> class, so that the underlying implementation
    /// can be replaced or extended transparently.
    /// </para>
    /// <para>
    /// The default implementation is <see cref="EventPublisher"/>, which provides
    /// a structured publish pipeline (enrich → validate → fan-out dispatch),
    /// typed-channel routing, and configurable error handling.
    /// </para>
    /// </remarks>
    public interface IEventPublisher
    {
        /// <summary>
        /// Publishes the given <see cref="CloudEvent"/> to the underlying channels.
        /// </summary>
        /// <param name="event">
        /// The <see cref="CloudEvent"/> instance to publish. The four mandatory
        /// CloudEvents 1.0 attributes (<c>id</c>, <c>source</c>, <c>type</c>,
        /// <c>specversion</c>) must be present (or filled in automatically by
        /// the implementation) before the event is dispatched.
        /// </param>
        /// <param name="options">
        /// An optional <see cref="EventPublishOptions"/> instance that overrides
        /// channel-level defaults for this single call. Pass a concrete options
        /// sub-type (e.g. <c>RabbitMqPublishOptions</c>) to target a specific
        /// channel, or a <see cref="CombinedPublishOptions"/> to target multiple
        /// channels with different overrides simultaneously.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> that completes when the event has been dispatched
        /// to all applicable channels.
        /// </returns>
        /// <exception cref="InvalidCloudEventException">
        /// Thrown when one or more required CloudEvents attributes are still absent
        /// after the implementation's enrichment step.
        /// </exception>
        Task PublishEventAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a <see cref="CloudEvent"/> from a event object whose type is only
        /// known at runtime and publishes it to the underlying channels.
        /// </summary>
        /// <param name="eventType">
        /// The <see cref="Type"/> of the event object. Must be decorated with
        /// <c>[Event]</c> annotations or implement <see cref="IEventConvertible"/>.
        /// </param>
        /// <param name="@event>
        /// The event object instance, or <c>null</c> if the event carries no payload.
        /// </param>
        /// <param name="options">
        /// An optional <see cref="EventPublishOptions"/> instance that overrides
        /// channel-level defaults for this single call.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> that completes when the event has been dispatched
        /// to all applicable channels.
        /// </returns>
        /// <exception cref="InvalidCloudEventException">
        /// Thrown when one or more required CloudEvents attributes are still absent
        /// after enrichment.
        /// </exception>
        Task PublishAsync(Type eventType, object? @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default);
    }
}
