//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Deveel.Events
{
    /// <summary>
    /// Extends the <see cref="EventPublisherBuilder"/> to add support for
    /// the MassTransit event publishing channel.
    /// </summary>
    public static class EventPublisherBuilderExtensions
    {
        private static EventPublisherBuilder AddMassTransitInfrastructure(this EventPublisherBuilder builder)
        {
            // No shared infrastructure beyond what the channel itself provides.
            return builder;
        }

        private static EventPublisherBuilder AddMassTransitChannel(this EventPublisherBuilder builder)
        {
            builder.AddMassTransitInfrastructure();
            // Register the concrete channel once under its own type so callers can resolve it
            // directly and supply per-call option overrides.
            builder.Services.TryAddSingleton<MassTransitEventPublishChannel>();
            // Expose it as IEventPublishChannel (type-based so ImplementationType is preserved).
            builder.Services.AddSingleton<IEventPublishChannel, MassTransitEventPublishChannel>();
            return builder;
        }

        /// <summary>
        /// Adds the MassTransit event publishing channel to the event publisher.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="configure">
        /// An action to configure the options for the MassTransit channel.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddMassTransit(
            this EventPublisherBuilder builder,
            Action<MassTransitEventPublishOptions>? configure = null)
        {
            var optBuilder = builder.Services.AddOptions<MassTransitEventPublishOptions>();
            if (configure is not null)
                optBuilder.Configure(configure);

            return builder.AddMassTransitChannel();
        }

        /// <summary>
        /// Adds the MassTransit event publishing channel to the event publisher,
        /// binding the options from the given configuration section.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="sectionPath">
        /// The configuration section path to bind the options for the MassTransit channel.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddMassTransit(
            this EventPublisherBuilder builder,
            string sectionPath)
        {
            builder.Services.AddOptions<MassTransitEventPublishOptions>()
                .BindConfiguration(sectionPath);

            return builder.AddMassTransitChannel();
        }

        /// <summary>
        /// Adds a typed MassTransit event publishing channel to the event publisher, so
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
        /// <see cref="MassTransitEventPublishOptions{TEvent}"/> for this channel.
        /// Non-<c>null</c> values override the corresponding properties from the general
        /// <see cref="MassTransitEventPublishOptions"/> (registered via
        /// <c>AddMassTransit(configure)</c>).
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddMassTransit<TEvent>(
            this EventPublisherBuilder builder,
            Action<MassTransitEventPublishOptions<TEvent>>? configure = null)
            where TEvent : class
        {
            var optBuilder = builder.Services.AddOptions<MassTransitEventPublishOptions<TEvent>>();
            if (configure is not null)
                optBuilder.Configure(configure);

            builder.AddMassTransitInfrastructure();
            return builder.AddChannel<MassTransitEventPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Adds a typed MassTransit event publishing channel to the event publisher, so
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
        /// <see cref="MassTransitEventPublishOptions{TEvent}"/> from.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddMassTransit<TEvent>(
            this EventPublisherBuilder builder,
            string sectionPath)
            where TEvent : class
        {
            builder.Services.AddOptions<MassTransitEventPublishOptions<TEvent>>()
                .BindConfiguration(sectionPath);

            builder.AddMassTransitInfrastructure();
            return builder.AddChannel<MassTransitEventPublishChannel<TEvent>, TEvent>();
        }
    }
}

