//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// Defines a service that publishes events to one or more channels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inject the default publisher directly as <see cref="IEventPublisher"/>.
    /// When multiple named pipelines are registered, resolve the desired instance
    /// using keyed DI: <c>IServiceProvider.GetRequiredKeyedService&lt;IEventPublisher&gt;(name)</c>.
    /// </para>
    /// </remarks>
    public interface IEventPublisher
    {
        /// <summary>
        /// Enriches and publishes the given <see cref="CloudEvent"/> to every
        /// registered channel, running the full middleware pipeline before fan-out.
        /// </summary>
        /// <param name="event">The <see cref="CloudEvent"/> to publish. Must not be <c>null</c>.</param>
        /// <param name="options">
        /// Optional per-call options that override the channel-level defaults.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task PublishEventAsync(CloudEvent @event, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a <see cref="CloudEvent"/> from the annotated data object and
        /// publishes it through the full middleware pipeline.
        /// </summary>
        /// <param name="eventType">
        /// The CLR type of the event data object. Must be decorated with
        /// <see cref="Hermodr.EventAttribute"/>.
        /// </param>
        /// <param name="event">
        /// The data object to convert and publish. May be <c>null</c> when the event
        /// carries no data payload.
        /// </param>
        /// <param name="options">Optional per-call options forwarded to the channel(s).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task PublishAsync(Type eventType, object? @event, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}

