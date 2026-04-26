//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Deveel.Events {
    /// <summary>
    /// A builder to configure the <see cref="EventPublisher"/> and its services.
    /// </summary>
    public sealed class EventPublisherBuilder {
		internal EventPublisherBuilder(IServiceCollection services) {
			Services = services;

			AddDefaultServices();
		}

        /// <summary>
        /// Gets the collection of services that are used to configure the publisher.
        /// </summary>
        public IServiceCollection Services { get; }

		private void AddDefaultServices() {
			Services.AddOptions<EventPublisherOptions>()
				.ValidateOnStart();
			Services.TryAddSingleton<IValidateOptions<EventPublisherOptions>>(_ => new EventPublisherOptionsValidator(Services));
			Services.TryAddSingleton<EventPublisher>();
			Services.TryAddSingleton<IEventIdGenerator>(EventGuidGenerator.Default);
			Services.TryAddSingleton<IEventSystemTime>(EventSystemTime.Instance);
			Services.TryAddSingleton<IEventCreator, EventCreator>();
		}

        /// <summary>
        /// Configures the options for the publisher using 
		/// the given action.
        /// </summary>
        /// <param name="configure">
		/// The action that configures the options for the publisher.
		/// </param>
        /// <returns>
		/// Returns the instance of the <see cref="EventPublisherBuilder"/> to
		/// further configure the publisher.
		/// </returns>
        public EventPublisherBuilder Configure(Action<EventPublisherOptions> configure) {
			Services.Configure(configure);

			return this;
		}

        /// <summary>
        /// Configures the options for the publisher using the
		/// configuration section at the given path within
		/// the application configuration.
        /// </summary>
        /// <param name="sectionPath">
		/// The path to the configuration section that contains the options
		/// to configure the publisher.
		/// </param>
        /// <returns>
		/// Returns the instance of the <see cref="EventPublisherBuilder"/> to
		/// further configure the publisher.
		/// </returns>
        public EventPublisherBuilder Configure(string sectionPath) {
			Services.AddOptions<EventPublisherOptions>()
				.BindConfiguration(sectionPath);

			return this;
		}

        /// <summary>
        /// Replace the default <see cref="EventPublisher"/> with the given type.
        /// </summary>
        /// <typeparam name="TPublisher">
		/// The type of the publisher to use instead of the default.
		/// </typeparam>
        /// <param name="lifetime">
		/// The lifetime of the service to register.
		/// </param>
        /// <returns>
		/// Returns the instance of the <see cref="EventPublisherBuilder"/> to
		/// further configure the publisher.
		/// </returns>
        public EventPublisherBuilder UsePublisher<TPublisher>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
			where TPublisher : EventPublisher {
			Services.RemoveAll<EventPublisher>();
			Services.Add(new ServiceDescriptor(typeof(EventPublisher), typeof(TPublisher), lifetime));

			return this;
		}

        /// <summary>
        /// Replace the default <see cref="IEventIdGenerator"/> with a generator
		/// that uses a GUID as the identifier of the events.
        /// </summary>
        /// <param name="format">
		/// The format to use for the GUIDs generated.
		/// </param>
        /// <returns>
		/// Returns the instance of the <see cref="EventPublisherBuilder"/> to
		/// further configure the publisher.
		/// </returns>
        public EventPublisherBuilder UseGuid(string? format = null) {
			Services.RemoveAll<IEventIdGenerator>();
			Services.AddSingleton<IEventIdGenerator, EventGuidGenerator>();

			Services.AddOptions<EventGuidGeneratorOptions>()
				.Configure(o => o.Format = format);

			return this;
		}

        /// <summary>
        /// Replace the default <see cref="IEventSystemTime"/> with the given type.
        /// </summary>
        /// <typeparam name="TSystemTime">
		/// The type of the system time to use for the events.
		/// </typeparam>
        /// <param name="lifetime">
		/// The lifetime of the service to register.
		/// </param>
        /// <returns>
		/// Returns the instance of the <see cref="EventPublisherBuilder"/> to
		/// further configure the publisher.
		/// </returns>
        public EventPublisherBuilder UseSystemTime<TSystemTime>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
			where TSystemTime : class, IEventSystemTime {
			Services.RemoveAll<IEventSystemTime>();
			Services.Add(new ServiceDescriptor(typeof(IEventSystemTime), typeof(TSystemTime), lifetime));

			return this;
		}

        /// <summary>
        /// Registers a publish channel of type <typeparamref name="TChannel"/>
        /// as an <see cref="IEventPublishChannel"/> that receives all events
        /// dispatched by the <see cref="EventPublisher"/>.
        /// </summary>
        /// <typeparam name="TChannel">
        /// The concrete channel type to register. Must implement
        /// <see cref="IEventPublishChannel"/>.
        /// </typeparam>
        /// <param name="lifetime">
        /// The <see cref="ServiceLifetime"/> of the channel registration.
        /// Defaults to <see cref="ServiceLifetime.Singleton"/>.
        /// </param>
        /// <returns>
        /// Returns this <see cref="EventPublisherBuilder"/> instance so that
        /// further calls can be chained.
        /// </returns>
        public EventPublisherBuilder AddChannel<TChannel>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TChannel : class, IEventPublishChannel
        {
            Services.TryAdd(new ServiceDescriptor(typeof(TChannel), typeof(TChannel), lifetime));
            Services.Add(new ServiceDescriptor(typeof(IEventPublishChannel),
                sp => sp.GetRequiredService<TChannel>(), lifetime));

            return this;
        }

        /// <summary>
        /// Registers a publish channel of type <typeparamref name="TChannel"/>
        /// as both an <see cref="IEventPublishChannel"/> and an
        /// <see cref="IEventPublishChannel{TEvent}"/>, so that the channel
        /// receives only events whose data class is <typeparamref name="TEvent"/>.
        /// </summary>
        /// <typeparam name="TChannel">
        /// The concrete channel type to register. Must implement
        /// <see cref="IEventPublishChannel{TEvent}"/>.
        /// </typeparam>
        /// <typeparam name="TEvent">
        /// The event data class this channel is keyed against.
        /// </typeparam>
        /// <param name="lifetime">
        /// The <see cref="ServiceLifetime"/> of the channel registration.
        /// Defaults to <see cref="ServiceLifetime.Singleton"/>.
        /// </param>
        /// <returns>
        /// Returns this <see cref="EventPublisherBuilder"/> instance so that
        /// further calls can be chained.
        /// </returns>
        public EventPublisherBuilder AddChannel<TChannel, TEvent>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TChannel : class, IEventPublishChannel<TEvent>
            where TEvent : class
        {
            Services.TryAdd(new ServiceDescriptor(typeof(TChannel), typeof(TChannel), lifetime));
            Services.Add(new ServiceDescriptor(typeof(IEventPublishChannel),
                sp => sp.GetRequiredService<TChannel>(), lifetime));
            Services.Add(new ServiceDescriptor(typeof(IEventPublishChannel<TEvent>),
                sp => sp.GetRequiredService<TChannel>(), lifetime));

            return this;
        }
	}
}
