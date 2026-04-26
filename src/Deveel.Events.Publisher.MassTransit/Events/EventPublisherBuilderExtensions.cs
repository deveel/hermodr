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
        private static EventPublisherBuilder AddMassTransitChannel(this EventPublisherBuilder builder)
        {
            // Register the concrete channel once; expose it under its own type so that
            // callers can resolve it directly and supply per-call option overrides, as
            // well as under IEventPublishChannel so EventPublisher can discover it.
            builder.Services.AddSingleton<MassTransitEventPublishChannel>();
            builder.Services.AddSingleton<IEventPublishChannel>(sp =>
                sp.GetRequiredService<MassTransitEventPublishChannel>());
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
    }
}

