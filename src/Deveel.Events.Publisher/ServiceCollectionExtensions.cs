//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Events;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel {
    /// <summary>
    /// Extensions for the <see cref="IServiceCollection"/> to add the event publisher
    /// into the service collection of an application.
    /// </summary>
    public static class ServiceCollectionExtensions {
        /// <summary>
        /// Adds the event publisher into the service collection of an application.
        /// </summary>
        /// <param name="services">
        /// The service collection to add the event publisher into.
        /// </param>
        /// <returns>
        /// Returns an instance of the <see cref="EventPublisherBuilder"/> that can be used
        /// to configure the event publisher.
        /// </returns>
        public static EventPublisherBuilder AddEventPublisher(this IServiceCollection services) {
			return new EventPublisherBuilder(services);
		}

        /// <summary>
        /// Adds the event publisher into the service collection of an application,
        /// using the configuration section with the given path to configure the publisher.
        /// </summary>
        /// <param name="services">
        /// The service collection to add the event publisher into.
        /// </param>
        /// <param name="sectionPath">
        /// The path to the configuration section that contains the settings for the 
        /// event publisher.
        /// </param>
        /// <returns>
        /// Returns an instance of the <see cref="EventPublisherBuilder"/> that can be used
        /// to configure the event publisher.
        /// </returns>
        public static EventPublisherBuilder AddEventPublisher(this IServiceCollection services, string sectionPath) {
			return new EventPublisherBuilder(services)
				.Configure(sectionPath);
		}

        /// <summary>
        /// Adds the event publisher into the service collection of an application,
        /// using the given configuration delegate to configure the publisher.
        /// </summary>
        /// <param name="services">
        /// The service collection to add the event publisher into.
        /// </param>
        /// <param name="configure">
        /// The delegate that is used to configure the event publisher.
        /// </param>
        /// <returns>
        /// Returns an instance of the <see cref="EventPublisherBuilder"/> that can be used
        /// to configure the event publisher.
        /// </returns>
		public static EventPublisherBuilder AddEventPublisher(this IServiceCollection services, Action<EventPublisherOptions> configure) {
			return new EventPublisherBuilder(services)
				.Configure(configure);
		}

        /// <summary>
        /// Adds a named event-publisher pipeline and configures it via
        /// <paramref name="configure"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="name">
        /// A unique name for this pipeline. Use <see cref="IEventPublisherFactory"/> to
        /// retrieve the named publisher at runtime.
        /// </param>
        /// <param name="configure">
        /// A delegate that configures the pipeline (channels, middleware, options).
        /// </param>
        public static EventPublisherBuilder AddEventPublisher(
            this IServiceCollection services,
            string name,
            Action<EventPublisherBuilder> configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            var builder = new EventPublisherBuilder(services, name);
            configure(builder);
            return builder;
        }
	}
}
