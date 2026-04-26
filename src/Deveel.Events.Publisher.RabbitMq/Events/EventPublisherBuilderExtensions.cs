//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using RabbitMQ.Client;

namespace Deveel.Events
{
    /// <summary>
    /// Extends the <see cref="EventPublisherBuilder"/> to add support for
    /// the RabbitMQ event publishing channel.
    /// </summary>
    public static class EventPublisherBuilderExtensions
    {
        private static EventPublisherBuilder AddRabbitMqInfrastructure(this EventPublisherBuilder builder)
        {
            builder.Services.TryAddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
            builder.Services.TryAddSingleton<IRabbitMqMessageFactory, RabbitMqMessageFactory>();
            builder.Services.TryAddSingleton<IConnection>(sp =>
            {
                var factory = sp.GetRequiredService<IRabbitMqConnectionFactory>();
                // CreateConnectionAsync is called synchronously here because DI factories are synchronous.
                // This is acceptable at startup time.
                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            });
            return builder;
        }

        private static EventPublisherBuilder AddRabbitMqChannel(this EventPublisherBuilder builder)
        {
            builder.AddRabbitMqInfrastructure();
            // Register the concrete channel once under its own type so callers can resolve it
            // directly and supply per-call option overrides.
            builder.Services.TryAddSingleton<RabbitMqEventPublishChannel>();
            // Expose it as IEventPublishChannel (type-based so ImplementationType is preserved).
            builder.Services.AddSingleton<IEventPublishChannel, RabbitMqEventPublishChannel>();
            return builder;
        }

        /// <summary>
        /// Adds the RabbitMQ event publishing channel to the event publisher.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="configure">
        /// An action to configure the options for the RabbitMQ channel.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddRabbitMq(this EventPublisherBuilder builder, Action<RabbitMqPublishOptions> configure)
        {
            builder.Services.AddOptions<RabbitMqPublishOptions>()
                .Configure(configure);

            return builder.AddRabbitMqChannel();
        }

        /// <summary>
        /// Adds the RabbitMQ event publishing channel to the event publisher.
        /// </summary>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="sectionPath">
        /// The configuration section path to bind the options for the RabbitMQ channel.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddRabbitMq(this EventPublisherBuilder builder, string sectionPath)
        {
            builder.Services.AddOptions<RabbitMqPublishOptions>()
                .BindConfiguration(sectionPath);

            return builder.AddRabbitMqChannel();
        }

        /// <summary>
        /// Adds a typed RabbitMQ event publishing channel to the event publisher, so
        /// that only events whose data class is <typeparamref name="TEvent"/> are
        /// routed to this channel.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event data class this channel is keyed against.
        /// </typeparam>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="configure">
        /// An action to configure the type-specific <see cref="RabbitMqPublishOptions{TEvent}"/>
        /// for this channel.  Non-<c>null</c> values override the corresponding properties
        /// from the general <see cref="RabbitMqPublishOptions"/> (registered via
        /// <c>AddRabbitMq(configure)</c>), enabling a two-level configuration hierarchy.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddRabbitMq<TEvent>(
            this EventPublisherBuilder builder,
            Action<RabbitMqPublishOptions<TEvent>> configure)
            where TEvent : class
        {
            builder.Services.AddOptions<RabbitMqPublishOptions<TEvent>>()
                .Configure(configure);

            builder.AddRabbitMqInfrastructure();
            return builder.AddChannel<RabbitMqEventPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Adds a typed RabbitMQ event publishing channel to the event publisher, so
        /// that only events whose data class is <typeparamref name="TEvent"/> are
        /// routed to this channel, binding options from the given configuration section.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event data class this channel is keyed against.
        /// </typeparam>
        /// <param name="builder">
        /// The <see cref="EventPublisherBuilder"/> to add the channel to.
        /// </param>
        /// <param name="sectionPath">
        /// The configuration section path to bind the type-specific
        /// <see cref="RabbitMqPublishOptions{TEvent}"/> from.
        /// </param>
        /// <returns>
        /// Returns the <see cref="EventPublisherBuilder"/> to continue the configuration.
        /// </returns>
        public static EventPublisherBuilder AddRabbitMq<TEvent>(
            this EventPublisherBuilder builder,
            string sectionPath)
            where TEvent : class
        {
            builder.Services.AddOptions<RabbitMqPublishOptions<TEvent>>()
                .BindConfiguration(sectionPath);

            builder.AddRabbitMqInfrastructure();
            return builder.AddChannel<RabbitMqEventPublishChannel<TEvent>, TEvent>();
        }
    }
}
