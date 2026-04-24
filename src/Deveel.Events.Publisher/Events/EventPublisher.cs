//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.ComponentModel.DataAnnotations;

namespace Deveel.Events {
    /// <summary>
    /// The service that is responsible for publishing events 
	/// to the configured channels.
    /// </summary>
    public class EventPublisher : IEventPublisher {
		private readonly IEnumerable<IEventPublishChannel> _channels;
		private readonly IEventSystemTime _systemTime;
		private readonly IEventIdGenerator _idGenerator;
		private readonly ILogger _logger;

        /// <summary>
        /// Constructs the publisher with the given options 
		/// and channels.
        /// </summary>
        /// <param name="options">
		/// The options that are used to configure the publisher.
		/// </param>
        /// <param name="channels">
		/// The list of channels that are used to publish the events.
		/// </param>
        /// <param name="eventCreator">
		/// An instance of the service that is used to create events.
		/// </param>
        /// <param name="idGenerator">
		/// A service that is used to generate the event identifiers.
		/// </param>
        /// <param name="systemTime">
		/// The object that is used to get the current system time.
		/// </param>
        /// <param name="logger">
		/// A logger that is used to log the events.
		/// </param>
        public EventPublisher(
			IOptions<EventPublisherOptions> options, 
			IEnumerable<IEventPublishChannel> channels,
			IEventCreator? eventCreator = null,
			IEventIdGenerator? idGenerator = null,
			IEventSystemTime? systemTime = null,
			ILogger<EventPublisher>? logger = null) {
			_channels = channels;
            EventCreator = eventCreator;
            _idGenerator = idGenerator ?? EventGuidGenerator.Default;
			_systemTime = systemTime ?? EventSystemTime.Instance;
			_logger = logger ?? NullLogger<EventPublisher>.Instance;
			PublisherOptions = options.Value;
		}

        /// <summary>
        /// Gets the options that are used to configure the publisher.
        /// </summary>
        protected EventPublisherOptions PublisherOptions { get; }

        /// <summary>
        /// Gets the service that is used to create events.
        /// </summary>
        protected IEventCreator? EventCreator { get; }

        /// <summary>
        /// Publishes the event to the given channel.
        /// </summary>
        /// <param name="channel">
		/// The instance of the channel that is used to 
		/// publish the event.
		/// </param>
        /// <param name="event">
		/// The event that is to be published.
		/// </param>
        /// <param name="cancellationToken">
		/// A token that is used to cancel the operation.
		/// </param>
        /// <returns>
		/// Returns a task that represents the asynchronous operation.
		/// </returns>
        protected virtual Task PublishEventAsync(IEventPublishChannel channel, CloudEvent @event, CancellationToken cancellationToken) {
			return channel.PublishAsync(@event, cancellationToken);
		}

        /// <summary>
        /// Sets the timestamp of the event if it was 
		/// not already set.
        /// </summary>
        /// <param name="event">
		/// The event to set the timestamp.
		/// </param>
		/// <remarks>
		/// This method uses the <see cref="IEventSystemTime"/> service
		/// to get the current system time to be used as the timestamp
		/// of the event.
		/// </remarks>
        /// <returns>
		/// Returns the event with the timestamp set.
		/// </returns>
        protected virtual CloudEvent SetTimeStamp(CloudEvent @event) {
			if (@event.Time == null)
				@event.Time = _systemTime.UtcNow;

			return @event;
		}

        /// <summary>
        /// Sets the source of the event if it was not already set.
        /// </summary>
        /// <param name="event">
		/// The event to set the source.
		/// </param>
		/// <remarks>
		/// This method uses the <see cref="EventPublisherOptions.Source"/>
		/// to set the source of the event.
		/// </remarks>
        /// <returns>
		/// Returns the event with the source set.
		/// </returns>
        protected virtual CloudEvent SetSource(CloudEvent @event) {
			if (@event.Source == null && PublisherOptions.Source != null)
				@event.Source = PublisherOptions.Source;

			return @event;
		}

        /// <summary>
        /// Sets the identifier of the event if it was not already set.
        /// </summary>
        /// <param name="event">
		/// The event to set the identifier.
		/// </param>
		/// <remarks>
		/// This method uses the <see cref="IEventIdGenerator"/> service
		/// to generate a new identifier for the event.
		/// </remarks>
        /// <returns>
		/// Returns the event with the identifier set.
		/// </returns>
        protected virtual CloudEvent SetEventId(CloudEvent @event) {
			if (@event.Id == null)
				@event.Id = _idGenerator.GenerateId();

			return @event;
		}

        /// <summary>
        /// Adds the attributes configured for the publisher 
		/// into the event.
        /// </summary>
        /// <param name="event">
		/// The event to add the attributes.
		/// </param>
        /// <returns>
		/// Returns the event with the attributes set.
		/// </returns>
        protected virtual CloudEvent SetAttributes(CloudEvent @event) {
			if (PublisherOptions.Attributes != null)
			{
				foreach (var attribute in PublisherOptions.Attributes)
				{
					var attr = CloudEventAttribute.CreateExtension(attribute.Key, GetAttributeType(attribute.Value));
					@event[attr] = attribute.Value;
                }
			}

			return @event;
		}

        private CloudEventAttributeType GetAttributeType(object? value)
        {
			return value switch
			{
				bool => CloudEventAttributeType.Boolean,
				byte[] _ => CloudEventAttributeType.Binary,
				string _ => CloudEventAttributeType.String,
				int _ => CloudEventAttributeType.Integer,
				long _ => CloudEventAttributeType.Integer,
				Uri _ => CloudEventAttributeType.Uri,
				DateTimeOffset _ => CloudEventAttributeType.Timestamp,
				DateTime _ => CloudEventAttributeType.Timestamp,
                _ => CloudEventAttributeType.String
			};

            throw new NotSupportedException($"Values of type {value.GetType()} are not supported");
        }

        /// <summary>
        /// Publishes the given event to all the configured channels.
        /// </summary>
        /// <param name="event">
		/// The event to publish.
		/// </param>
        /// <param name="cancellationToken">
		/// A token that is used to cancel the operation.
		/// </param>
        /// <returns>
		/// Returns a task that represents the asynchronous operation.
		/// </returns>
        /// <exception cref="EventPublishException">
		/// Thrown when an error occurs while publishing the event,
		/// and the <see cref="EventPublisherOptions.ThrowOnErrors"/> 
		/// is set to <c>true</c>.
		/// </exception>
        public async Task PublishEventAsync(CloudEvent @event, CancellationToken cancellationToken = default) {
			// TODO: validate the event before publishing

			var eventToPublish = @event;
			@event = SetEventId(@event);
			@event = SetTimeStamp(@event);
			@event = SetSource(@event);
			@event = SetAttributes(@event);

			foreach (var channel in _channels) {
				_logger.TraceEventPublishing(@event.Type!, channel.GetType());

				try {
					await PublishEventAsync(channel, @eventToPublish, cancellationToken);

					_logger.TraceEventPublished(@event.Type!, channel.GetType());
				} catch (Exception ex) {
					_logger.LogEventPublishError(ex, @event.Type!, channel.GetType());
					
					if (PublisherOptions.ThrowOnErrors) {
						throw new EventPublishException($"An error occurred while publishing an event of type {@event.Type} to the channel '{channel.GetType().Name}'", ex);
					}
				}
			}
		}

        /// <summary>
        /// Publishes an event that is created from the given 
		/// data type and the instance of the data.
        /// </summary>
        /// <param name="dataType">
		/// The type of the data that is used to create the event.
		/// </param>
        /// <param name="data">
		/// The instance of the data contained in the event.
		/// </param>
        /// <param name="cancellationToken">
		/// A token that is used to cancel the operation.
		/// </param>
        /// <returns>
		/// Returns a task that represents the asynchronous operation.
		/// </returns>
        /// <exception cref="EventPublishException">
		/// Thrown when an error occurs while creating the event from the data,
		/// and the <see cref="EventPublisherOptions.ThrowOnErrors"/>
		/// is set to <c>true</c>.
		/// </exception>
		/// <seealso cref="PublishEventAsync(CloudEvent, CancellationToken)"/>
        public Task PublishAsync(Type dataType, object? data, CancellationToken cancellationToken = default) {
			CloudEvent @event;

			try {
				@event = CreateEventFromData(dataType, data);
			} catch (Exception ex) {
				_logger.LogEventCreateError(ex, dataType);

				if (PublisherOptions.ThrowOnErrors)
					throw new EventPublishException($"An error occurred while creating an event of type {dataType.FullName} from the provided data", ex);

				return Task.CompletedTask;
			}
			
			return PublishEventAsync(@event, cancellationToken);
		}

        /// <summary>
        /// Creates an event from the given data type and 
		/// the instance of the data.
        /// </summary>
        /// <param name="dataType">
		/// The type of the data that is used to create the event.
		/// </param>
        /// <param name="data">
		/// The instance of the data contained in the event.
		/// </param>
        /// <returns>
		/// Returns the created event from the data.
		/// </returns>
        /// <exception cref="NotSupportedException">
		/// Thrown when the event creator is not set.
		/// </exception>
        protected virtual CloudEvent CreateEventFromData(Type dataType, object? data)
        {
            if (EventCreator == null)
                throw new NotSupportedException("Cannot create events from the data");

			return EventCreator.CreateEventFromData(dataType, data);
        }

        /// <summary>
        /// Publishes an event of the given type of data.
        /// </summary>
        /// <typeparam name="TData">
		/// The type of the data that is used to create the event.
		/// </typeparam>
        /// <param name="data">
		/// The instance of the data contained in the event.
		/// </param>
        /// <param name="cancellationToken">
		/// A token that is used to cancel the operation.
		/// </param>
        /// <returns>
		/// Returns a task that represents the asynchronous operation.
		/// </returns>
        public Task PublishAsync<TData>(TData data, CancellationToken cancellationToken = default)
			=> PublishAsync(typeof(TData), data, cancellationToken);

        /// <summary>
        /// Publishes an event that is created from the given 
		/// factory instance.
        /// </summary>
        /// <typeparam name="T">
		/// The type of the factory that is used to create the event.
		/// </typeparam>
        /// <param name="factory">
		/// The instance of the factory that is used to create the event.
		/// </param>
        /// <param name="cancellationToken">
		/// A token that is used to cancel the operation.
		/// </param>
        /// <returns>
        /// Returns a task that represents the asynchronous publish operation.
        /// </returns>
        /// <exception cref="EventPublishException">
        /// Thrown when the factory fails to create the event or an error occurs
        /// while publishing, and <see cref="EventPublisherOptions.ThrowOnErrors"/>
        /// is set to <c>true</c>.
        /// </exception>
        public Task PublishEventAsync<T>(T factory, CancellationToken cancellationToken = default)
			where T : IEventFactory
        {
            ArgumentNullException.ThrowIfNull(factory, nameof(factory));

            CloudEvent @event;

            try
            {
                @event = factory.CreateEvent();
            } catch (Exception ex)
            {
                _logger.LogEventFactoryError(ex, factory.GetType());

                if (PublisherOptions.ThrowOnErrors)
                    throw new EventPublishException($"An error occurred while creating an event using the factory {factory.GetType().FullName}", ex);

                return Task.CompletedTask;
            }

            return PublishEventAsync(@event, cancellationToken);
        }
    }
}
