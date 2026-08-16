//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr
{
    /// <summary>
    /// Extends the <see cref="EventPublisherBuilder"/> to add support for
    /// the Dapr event publishing channel (publishes CloudEvents through the
    /// Dapr pub/sub building block on top of <see cref="DaprClient"/>).
    /// </summary>
    /// <remarks>
    /// The Dapr channel is the portable analogue of the direct broker adapters
    /// (RabbitMQ, Azure Service Bus, ...): write-once-run-anywhere publish
    /// against any Dapr-supported broker, while keeping the full Hermodr
    /// pipeline (enrichment, schema validation, middleware, dead-letter,
    /// tracing, delivery log) intact.
    /// </remarks>
    public static class EventPublisherBuilderExtensions
    {
        private static EventPublisherBuilder AddDaprInfrastructure(this EventPublisherBuilder builder)
        {
            // Register the DaprClient factory. The host is expected to have called
            // AddDaprClient() (from the Dapr SDK) so a DaprClient is available in
            // the container; when it is not, a default client is built on demand
            // from the standard Dapr SDK builder surface.
            builder.Services.TryAddSingleton<DaprClient>(sp =>
                new Dapr.Client.DaprClientBuilder().Build());
            builder.Services.TryAddSingleton<IDaprClientBuilder, DaprClientBuilder>();
            return builder;
        }

        private static EventPublisherBuilder AddDaprChannel(this EventPublisherBuilder builder)
        {
            builder.AddDaprInfrastructure();
            // Register the concrete channel and expose it as a keyed IEventPublishChannel
            // scoped to this publisher pipeline.
            return builder.AddChannel<DaprPublishChannel>();
        }

        /// <summary>
        /// Adds the Dapr event publishing channel to the event publisher.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="configure">
        /// An action to configure the options for the Dapr channel.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddDapr(
            this EventPublisherBuilder builder,
            Action<DaprPublishOptions>? configure = null)
        {
            var optBuilder = builder.Services.AddOptions<DaprPublishOptions>();
            if (configure is not null)
                optBuilder.Configure(configure);

            return builder.AddDaprChannel();
        }

        /// <summary>
        /// Adds the Dapr event publishing channel to the event publisher,
        /// binding the options from the given configuration section.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="sectionPath">
        /// The configuration section path to bind the options for the Dapr channel.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddDapr(
            this EventPublisherBuilder builder,
            string sectionPath)
        {
            builder.Services.AddOptions<DaprPublishOptions>()
                .BindConfiguration(sectionPath);

            return builder.AddDaprChannel();
        }

        /// <summary>
        /// Adds a typed Dapr event publishing channel to the event publisher, so
        /// that only events whose data class is <typeparamref name="TEvent"/> are routed
        /// to this channel.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event data class this channel is keyed against.
        /// </typeparam>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="configure">
        /// An optional action to configure the type-specific
        /// <see cref="DaprPublishOptions{TEvent}"/> for this channel.
        /// Non-<c>null</c> values override the corresponding properties from the general
        /// <see cref="DaprPublishOptions"/> (registered via <c>AddDapr(configure)</c>).
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddDapr<TEvent>(
            this EventPublisherBuilder builder,
            Action<DaprPublishOptions<TEvent>>? configure = null)
            where TEvent : class
        {
            var optBuilder = builder.Services.AddOptions<DaprPublishOptions<TEvent>>();
            if (configure is not null)
                optBuilder.Configure(configure);

            builder.AddDaprInfrastructure();
            return builder.AddChannel<DaprPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Adds a typed Dapr event publishing channel to the event publisher, so
        /// that only events whose data class is <typeparamref name="TEvent"/> are routed
        /// to this channel, binding options from the given configuration section.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event data class this channel is keyed against.
        /// </typeparam>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="sectionPath">
        /// The configuration section path to bind the type-specific
        /// <see cref="DaprPublishOptions{TEvent}"/> from.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddDapr<TEvent>(
            this EventPublisherBuilder builder,
            string sectionPath)
            where TEvent : class
        {
            builder.Services.AddOptions<DaprPublishOptions<TEvent>>()
                .BindConfiguration(sectionPath);

            builder.AddDaprInfrastructure();
            return builder.AddChannel<DaprPublishChannel<TEvent>, TEvent>();
        }
    }
}