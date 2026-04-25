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
        private static EventPublisherBuilder AddServiceBusChannel(this EventPublisherBuilder builder) {
			builder.Services.AddSingleton<IEventPublishChannel, ServiceBusEventPublishChannel>();
			builder.Services.AddSingleton<IEventPublishChannel<ServiceBusEventPublishChannelOptions>, ServiceBusEventPublishChannel>();
			builder.Services.TryAddSingleton<IServiceBusClientFactory, ServiceBusClientFactory>();
			builder.Services.TryAddSingleton<ServiceBusMessageFactory>();
			return builder;
		}

        /// <summary>
        /// Adds a channel to the event publisher that publishes events
		/// to an Azure Service Bus.
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
			builder.Services.AddOptions<ServiceBusEventPublishChannelOptions>()
				.BindConfiguration(sectionPath)
				.PostConfigure<IOptions<EventPublisherOptions>>(ConfigureIdentifier);

			return builder;
		}

        /// <summary>
        /// Adds a channel to the event publisher that publishes events
        /// to an Azure Service Bus.
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
        public static EventPublisherBuilder AddServiceBusChannel(this EventPublisherBuilder builder, Action<ServiceBusEventPublishChannelOptions> configure) {
			builder.AddServiceBusChannel();
			builder.Services.AddOptions<ServiceBusEventPublishChannelOptions>()
				.Configure(configure)
				.PostConfigure<IOptions<EventPublisherOptions>>(ConfigureIdentifier);

			return builder;
		}

		private static void ConfigureIdentifier(ServiceBusEventPublishChannelOptions channelOptions, IOptions<EventPublisherOptions> publisherOptions) { 
			if (String.IsNullOrWhiteSpace(channelOptions.ClientOptions.Identifier))
				channelOptions.ClientOptions.Identifier = publisherOptions?.Value.Source?.ToString() ?? "";
		}
	}
}
