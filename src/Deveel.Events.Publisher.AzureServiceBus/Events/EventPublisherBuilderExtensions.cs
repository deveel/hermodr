//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Deveel.Events {
    /// <summary>
    /// Extensions for the <see cref="EventPublisherBuilder"/> to add a channel
    /// publishing events to an Azure Service Bus.
    /// </summary>
    public static class EventPublisherBuilderExtensions {
        private static EventPublisherBuilder AddServiceBusInfrastructure(this EventPublisherBuilder builder) {
            builder.Services.TryAddSingleton<IServiceBusClientFactory, ServiceBusClientFactory>();
            builder.Services.TryAddSingleton<ServiceBusMessageFactory>();
            return builder;
        }

        private static EventPublisherBuilder AddServiceBusChannel(this EventPublisherBuilder builder) {
            builder.AddServiceBusInfrastructure();
            // Register the concrete channel once under its own type so callers can resolve it
            // directly and supply per-call option overrides.
            builder.Services.TryAddSingleton<ServiceBusEventPublishChannel>();
            // Expose it as IEventPublishChannel (type-based so ImplementationType is preserved).
			builder.Services.AddSingleton<IEventPublishChannel, ServiceBusEventPublishChannel>();
			return builder;
		}

        /// <summary>
        /// Adds a channel to the event publisher that publishes events
        /// to an Azure Service Bus, binding options from the given configuration section.
        /// </summary>
        /// <param name="builder">
        /// The instance of the <see cref="EventPublisherBuilder"/> that is
        /// used to configure the publisher.
        /// </param>
        /// <param name="sectionPath">
        /// The path to the configuration section that contains the options
        /// to configure the channel.
        /// </param>
        /// <returns>
        /// Returns the instance of the <see cref="EventPublisherBuilder"/> to
        /// continue the configuration of the publisher.
        /// </returns>
        public static EventPublisherBuilder AddServiceBusChannel(this EventPublisherBuilder builder, string sectionPath) {
			builder.AddServiceBusChannel();
			builder.Services.AddOptions<ServiceBusEventPublishOptions>()
				.BindConfiguration(sectionPath)
				.PostConfigure<IOptions<EventPublisherOptions>>(ConfigureIdentifier);

			return builder;
		}

        /// <summary>
        /// Adds a channel to the event publisher that publishes events
        /// to an Azure Service Bus, configured with the given action.
        /// </summary>
        /// <param name="builder">
        /// The instance of the <see cref="EventPublisherBuilder"/> that is
        /// used to configure the publisher.
        /// </param>
        /// <param name="configure">
        /// The action that configures the options for the channel.
        /// </param>
        /// <returns>
        /// Returns the instance of the <see cref="EventPublisherBuilder"/> to
        /// continue the configuration of the publisher.
        /// </returns>
        public static EventPublisherBuilder AddServiceBusChannel(this EventPublisherBuilder builder, Action<ServiceBusEventPublishOptions> configure) {
			builder.AddServiceBusChannel();
			builder.Services.AddOptions<ServiceBusEventPublishOptions>()
				.Configure(configure)
				.PostConfigure<IOptions<EventPublisherOptions>>(ConfigureIdentifier);

			return builder;
		}

        /// <summary>
        /// Adds a typed Azure Service Bus channel to the event publisher, so that only
        /// events whose data class is <typeparamref name="TEvent"/> are routed to this channel.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event data class this channel is keyed against.
        /// </typeparam>
        /// <param name="builder">
        /// The instance of the <see cref="EventPublisherBuilder"/> that is
        /// used to configure the publisher.
        /// </param>
        /// <param name="sectionPath">
        /// The path to the configuration section that contains the type-specific
        /// <see cref="ServiceBusEventPublishOptions{TEvent}"/> to bind.
        /// </param>
        /// <returns>
        /// Returns the instance of the <see cref="EventPublisherBuilder"/> to
        /// continue the configuration of the publisher.
        /// </returns>
        public static EventPublisherBuilder AddServiceBusChannel<TEvent>(
            this EventPublisherBuilder builder,
            string sectionPath)
            where TEvent : class
        {
            builder.AddServiceBusInfrastructure();
            builder.Services.AddOptions<ServiceBusEventPublishOptions<TEvent>>()
                .BindConfiguration(sectionPath)
                .PostConfigure<IOptions<EventPublisherOptions>>(ConfigureIdentifierTyped<TEvent>);

            return builder.AddChannel<ServiceBusEventPublishChannel<TEvent>, TEvent>();
        }

        /// <summary>
        /// Adds a typed Azure Service Bus channel to the event publisher, so that only
        /// events whose data class is <typeparamref name="TEvent"/> are routed to this channel.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event data class this channel is keyed against.
        /// </typeparam>
        /// <param name="builder">
        /// The instance of the <see cref="EventPublisherBuilder"/> that is
        /// used to configure the publisher.
        /// </param>
        /// <param name="configure">
        /// The action that configures the type-specific
        /// <see cref="ServiceBusEventPublishOptions{TEvent}"/> for this channel.
        /// Non-empty / non-<c>null</c> values override the corresponding base channel options.
        /// </param>
        /// <returns>
        /// Returns the instance of the <see cref="EventPublisherBuilder"/> to
        /// continue the configuration of the publisher.
        /// </returns>
        public static EventPublisherBuilder AddServiceBusChannel<TEvent>(
            this EventPublisherBuilder builder,
            Action<ServiceBusEventPublishOptions<TEvent>> configure)
            where TEvent : class
        {
            builder.AddServiceBusInfrastructure();
            builder.Services.AddOptions<ServiceBusEventPublishOptions<TEvent>>()
                .Configure(configure)
                .PostConfigure<IOptions<EventPublisherOptions>>(ConfigureIdentifierTyped<TEvent>);

            return builder.AddChannel<ServiceBusEventPublishChannel<TEvent>, TEvent>();
        }

		private static void ConfigureIdentifier(ServiceBusEventPublishOptions channelOptions, IOptions<EventPublisherOptions> publisherOptions) { 
			if (String.IsNullOrWhiteSpace(channelOptions.ClientOptions.Identifier))
				channelOptions.ClientOptions.Identifier = publisherOptions?.Value.Source?.ToString() ?? "";
		}

        private static void ConfigureIdentifierTyped<TEvent>(ServiceBusEventPublishOptions<TEvent> channelOptions, IOptions<EventPublisherOptions> publisherOptions)
            where TEvent : class
        {
            // Only set the identifier on the typed options when the caller hasn't set one explicitly,
            // and only when a non-null ClientOptions override is present (otherwise the base options handle it).
            if (channelOptions.ClientOptions != null &&
                string.IsNullOrWhiteSpace(channelOptions.ClientOptions.Identifier))
                channelOptions.ClientOptions.Identifier = publisherOptions?.Value.Source?.ToString() ?? "";
        }
	}
}
