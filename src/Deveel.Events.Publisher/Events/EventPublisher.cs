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
	public class EventPublisher : IEventPublisher
	{
		private readonly IEnumerable<IEventPublishChannel> _channels;
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
			ILogger<EventPublisher>? logger = null)
		{
			PublisherOptions = options == null ? new EventPublisherOptions() : options.Value;
			IdGenerator = idGenerator;
			SystemTime = systemTime;
			_channels = channels;
			EventCreator = eventCreator;
			_logger = logger ?? NullLogger<EventPublisher>.Instance;
		}

		/// <summary>
		/// Gets the service that is used to create events.
		/// </summary>
		protected IEventCreator? EventCreator { get; }
		
		    /// <summary>
    /// Gets the options that are used to configure the publisher.
    /// </summary>
    protected EventPublisherOptions PublisherOptions { get; }
    
    protected IEventSystemTime? SystemTime { get; }
    
    protected IEventIdGenerator? IdGenerator { get; }

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
        if (@event.Time == null && SystemTime != null)
            @event.Time = SystemTime.UtcNow;

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
        if (@event.Id == null && IdGenerator != null)
            @event.Id = IdGenerator.GenerateId();

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
    }


    /// <summary>
    /// Validates that the given event satisfies the four mandatory CloudEvents
    /// attributes (<c>id</c>, <c>source</c>, <c>type</c>, <c>specversion</c>)
    /// after enrichment and before the event is dispatched to any channel.
    /// </summary>
    /// <param name="event">
    /// The enriched event to validate.
    /// </param>
    /// <exception cref="InvalidCloudEventException">
    /// Thrown when one or more required CloudEvents attributes are absent or empty.
    /// </exception>
    protected virtual void ValidateCloudEvent(CloudEvent @event) {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(@event.Id))
            missing.Add("id");

        if (@event.Source == null)
            missing.Add("source");

        if (string.IsNullOrWhiteSpace(@event.Type))
            missing.Add("type");

        // specversion is always "1.0" when using the CloudNative SDK, but we
        // check it explicitly so that any unexpected null surfaces clearly.
        if (string.IsNullOrWhiteSpace(@event.SpecVersion?.VersionId))
            missing.Add("specversion");

        if (missing.Count > 0)
            throw new InvalidCloudEventException(missing);
    }


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
		protected virtual Task PublishEventAsync(IEventPublishChannel channel, CloudEvent @event, EventPublishChannelOptions? options = null,
			CancellationToken cancellationToken = default)
		{
			return channel.PublishAsync(@event, options, cancellationToken);
		}
		
		private CloudEvent EnsureEvent(CloudEvent @event) {
			ArgumentNullException.ThrowIfNull(nameof(@event));

			var eventToPublish = @event;
			eventToPublish = SetEventId(eventToPublish);
			eventToPublish = SetTimeStamp(eventToPublish);
			eventToPublish = SetSource(eventToPublish);
			eventToPublish = SetAttributes(eventToPublish);
			
			return eventToPublish;
		}

		private async Task PublishEventToChannelsAsync(IEnumerable<IEventPublishChannel> channels, CloudEvent @event, EventPublishChannelOptions? options = null, CancellationToken cancellationToken = default)
		{
			var eventToPublish = EnsureEvent(@event);
			
			ValidateCloudEvent(@eventToPublish);
			
			foreach (var channel in channels)
			{
				_logger.TraceEventPublishing(@event.Type!, channel.GetType());

				try
				{
					await PublishEventAsync(channel, @eventToPublish, options, cancellationToken);

					_logger.TraceEventPublished(@event.Type!, channel.GetType());
				}
				catch (Exception ex)
				{
					_logger.LogEventPublishError(ex, @event.Type!, channel.GetType());

					if (PublisherOptions.ThrowOnErrors)
					{
						throw new EventPublishException(
							$"An error occurred while publishing an event of type {@event.Type} to the channel '{channel.GetType().Name}'",
							ex);
					}
				}
			}
		}
		
		/// <inheritdoc cref="IEventPublisher.PublishEventAsync"/>
		/// <exception cref="InvalidCloudEventException">
		/// Thrown when any of the required CloudEvents attributes (<c>id</c>,
		/// <c>source</c>, <c>type</c>, <c>specversion</c>) is still absent after
		/// enrichment.
		/// </exception>
		public virtual Task PublishEventAsync(CloudEvent @event, EventPublishChannelOptions? options = null, CancellationToken cancellationToken = default)
		{
			return PublishEventToChannelsAsync(_channels, @event, options, cancellationToken);
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
		/// <seealso cref="PublishEventAsync(CloudEvent, EventPublishChannelOptions, CancellationToken)"/>
		public Task PublishAsync(Type dataType, object? data, EventPublishChannelOptions? options = null, CancellationToken cancellationToken = default)
		{
			CloudEvent @event;

			try
			{
				@event = CreateEventFromData(dataType, data);
			}
			catch (Exception ex)
			{
				_logger.LogEventCreateError(ex, dataType);

				if (PublisherOptions.ThrowOnErrors)
					throw new EventPublishException(
						$"An error occurred while creating an event of type {dataType.FullName} from the provided data",
						ex);

				return Task.CompletedTask;
			}

			// First let's try to resolve event publishers specific for this
			// type of object...
			
			var typedChannels = GetTypedChannels(dataType);
			if (typedChannels.Count > 0)
			{
				return PublishEventToChannelsAsync(typedChannels, @event, options, cancellationToken);
			}

			return PublishEventAsync(@event, options, cancellationToken);
		}

		private IReadOnlyList<IEventPublishChannel> GetTypedChannels(Type dataType)
		{
			// TODO: cache this result
			var typedChannel = typeof(IEventPublishChannel<>).MakeGenericType(dataType);
			return _channels.Where(typedChannel.IsInstanceOfType).ToList();
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
		public Task PublishAsync<TData>(TData data, EventPublishChannelOptions? options = null, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(data);

			if (data is IEventConvertible eventConvertible)
			{
				CloudEvent  @event = null;
				try
				{
					@event = eventConvertible.ToCloudEvent();
				}
				catch (Exception ex)
				{
					_logger.LogEventCreateError(ex, typeof(TData));
					
					if (PublisherOptions.ThrowOnErrors)
						throw new EventPublishException("An error occurred while converting data", ex);
				}
				
				if (@event != null)
					return PublishEventAsync(@event, options, cancellationToken);
				
				return Task.CompletedTask;
			}
			

			if (data is CloudEvent cloudEvent)
				return PublishEventAsync(cloudEvent, options, cancellationToken);

			return PublishAsync(typeof(TData), data,  options, cancellationToken);
		}
	}
}
