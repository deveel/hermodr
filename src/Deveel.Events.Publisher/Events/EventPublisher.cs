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
		private Dictionary<Type, IReadOnlyList<IEventPublishChannel>>? _typedChannelCache;

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
        /// Resolves the options to pass to the given channel.  If <paramref name="options"/>
        /// is not compatible with the options type declared by the channel (i.e. the channel
        /// extends <see cref="EventPublishChannelBase{TOptions}"/> with a different
        /// <c>TOptions</c>), <c>null</c> is returned so that the channel falls back to its
        /// registered defaults.
        /// </summary>
        /// <param name="channel">The target channel.</param>
        /// <param name="options">The caller-supplied per-call options, or <c>null</c>.</param>
        /// <returns>
        /// <paramref name="options"/> when compatible with the channel; otherwise <c>null</c>.
        /// </returns>
        protected virtual EventPublishOptions? ResolveChannelOptions(IEventPublishChannel channel, EventPublishOptions? options)
        {
            if (options == null)
                return null;

            // Walk the inheritance chain looking for EventPublishChannelBase<TOptions>.
            var channelType = channel.GetType();
            for (var t = channelType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (!t.IsGenericType)
                    continue;

                if (t.GetGenericTypeDefinition() != typeof(EventPublishChannelBase<>))
                    continue;

                // Found the base – determine the channel's expected options type.
                var expectedOptionsType = t.GetGenericArguments()[0];

                // When the caller supplied a CombinedEventPublishOptions, pick the first
                // bundled entry that is compatible with this channel's TOptions.
                if (options is CombinedEventPublishOptions combined)
                    return combined.GetOptions(expectedOptionsType);

                // Otherwise fall back to the original single-options compatibility check.
                return expectedOptionsType.IsInstanceOfType(options) ? options : null;
            }

            // Channel does not derive from EventPublishChannelBase<TOptions> – pass null.
            return null;
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
        /// <param name="options">
        /// Optional per-call publish options.  The publisher checks whether the channel
        /// supports the concrete options type before forwarding; if the types are
        /// incompatible <c>null</c> is passed and the channel uses its registered defaults.
        /// </param>
        /// <param name="cancellationToken">
		/// A token that is used to cancel the operation.
		/// </param>
        /// <returns>
		/// Returns a task that represents the asynchronous operation.
		/// </returns>
        protected virtual Task PublishEventAsync(IEventPublishChannel channel, CloudEvent @event, EventPublishOptions? options, CancellationToken cancellationToken) {
			var resolvedOptions = ResolveChannelOptions(channel, options);
			return channel.PublishAsync(@event, resolvedOptions, cancellationToken);
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
        /// Enriches the given event by setting the identifier, timestamp,
        /// source, and any configured extension attributes, then validates that
        /// all required CloudEvents attributes are present.
        /// </summary>
        /// <param name="event">The raw event to prepare.</param>
        /// <returns>The enriched and validated event.</returns>
        /// <exception cref="InvalidCloudEventException">
        /// Thrown when a required CloudEvents attribute is missing after enrichment.
        /// </exception>
        protected virtual CloudEvent PrepareEvent(CloudEvent @event)
        {
            @event = SetEventId(@event);
            @event = SetTimeStamp(@event);
            @event = SetSource(@event);
            @event = SetAttributes(@event);
            ValidateCloudEvent(@event);
            return @event;
        }

        /// <summary>
        /// Returns the <see cref="IEventPublishChannel"/> instances that are
        /// registered as <c>IEventPublishChannel&lt;<paramref name="eventType"/>&gt;</c>.
        /// The result is cached so that the reflection cost is paid only once per type.
        /// </summary>
        /// <param name="eventType">The event data type to look up.</param>
        /// <returns>
        /// A (possibly empty) read-only list of channels keyed to
        /// <paramref name="eventType"/>.
        /// </returns>
        protected IReadOnlyList<IEventPublishChannel> GetTypedChannels(Type eventType)
        {
            _typedChannelCache ??= new Dictionary<Type, IReadOnlyList<IEventPublishChannel>>();

            if (!_typedChannelCache.TryGetValue(eventType, out var typed))
            {
                var channelType = typeof(IEventPublishChannel<>).MakeGenericType(eventType);
                typed = _channels.Where(channelType.IsInstanceOfType).ToList();
                _typedChannelCache[eventType] = typed;
            }

            return typed;
        }

        /// <summary>
        /// Dispatches the already-enriched <paramref name="event"/> to each channel
        /// in <paramref name="channels"/> in sequence.
        /// </summary>
        /// <param name="event">The enriched event to dispatch.</param>
        /// <param name="channels">The channels to publish to.</param>
        /// <param name="options">Optional per-call publish options.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        protected virtual async Task PublishEventToChannelsAsync(
            CloudEvent @event,
            IEnumerable<IEventPublishChannel> channels,
            EventPublishOptions? options,
            CancellationToken cancellationToken)
        {
            foreach (var channel in channels)
            {
                _logger.TraceEventPublishing(@event.Type!, channel.GetType());

                try
                {
                    await PublishEventAsync(channel, @event, options, cancellationToken);

                    _logger.TraceEventPublished(@event.Type!, channel.GetType());
                }
                catch (Exception ex)
                {
                    _logger.LogEventPublishError(ex, @event.Type!, channel.GetType());

                    if (PublisherOptions.ThrowOnErrors)
                        throw new EventPublishException(
                            $"An error occurred while publishing an event of type {@event.Type} to the channel '{channel.GetType().Name}'",
                            ex);
                }
            }
        }

        /// <inheritdoc cref="IEventPublisher.PublishEventAsync"/>
        /// <exception cref="InvalidCloudEventException">
        /// Thrown when any of the required CloudEvents attributes (<c>id</c>,
        /// <c>source</c>, <c>type</c>, <c>specversion</c>) is still absent after
        /// enrichment.
        /// </exception>
        public async Task PublishEventAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default) {
			var eventToPublish = PrepareEvent(@event);
			await PublishEventToChannelsAsync(eventToPublish, _channels, options, cancellationToken);
		}
        
        /// <summary>
        /// Publishes an event that is created from the given 
		/// data type and the instance of the data.
        /// </summary>
        /// <param name="eventType">
		/// The type of the data that is used to create the event.
		/// </param>
        /// <param name="data">
		/// The instance of the data contained in the event.
		/// </param>
        /// <param name="options">
        /// Optional per-call publish options forwarded to compatible channels.
        /// Channels that do not recognise the options type receive <c>null</c> and
        /// fall back to their registered defaults.
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
		/// <seealso cref="PublishEventAsync(CloudEvent, EventPublishOptions, CancellationToken)"/>
        public Task PublishAsync(Type eventType, object? data, EventPublishOptions? options = null, CancellationToken cancellationToken = default) {
			CloudEvent @event;

			try {
				@event = CreateEventFromData(eventType, data);
			} catch (Exception ex) {
				_logger.LogEventCreateError(ex, eventType);

				if (PublisherOptions.ThrowOnErrors)
					throw new EventPublishException($"An error occurred while creating an event of type {eventType.FullName} from the provided data", ex);

				return Task.CompletedTask;
			}

			// Prefer channels registered specifically for this event data type;
			// fall back to the general untyped channels when none are found.
			var typedChannels = GetTypedChannels(eventType);
			var channels = typedChannels.Count > 0
				? (IEnumerable<IEventPublishChannel>)typedChannels
				: _channels;

			var eventToPublish = PrepareEvent(@event);
			return PublishEventToChannelsAsync(eventToPublish, channels, options, cancellationToken);
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
	        ArgumentNullException.ThrowIfNull(dataType, nameof(dataType));
	        ArgumentNullException.ThrowIfNull(data, nameof(data));
	        
	        if (typeof(IEventConvertible).IsAssignableFrom(dataType))
		        return ((IEventConvertible)data!).ToEvent();
	        
            if (EventCreator == null)
                throw new NotSupportedException("Cannot create events from the data");

			return EventCreator.CreateEventFromData(dataType, data);
        }

        /// <summary>
        /// Publishes an event of the given typeevent.
        /// </summary>
        /// <typeparam name="TEvent">
		/// The type of event the event to publish.
		/// </typeparam>
        /// <param name="event">
		/// The instance of the event to publish.
		/// </param>
        /// <param name="options">
        /// Optional per-call publish options forwarded to compatible channels.
        /// Channels that do not recognise the options type receive <c>null</c> and
        /// fall back to their registered defaults.
        /// </param>
        /// <param name="cancellationToken">
		/// A token that is used to cancel the operation.
		/// </param>
        /// <returns>
		/// Returns a task that represents the asynchronous operation.
		/// </returns>
        public Task PublishAsync<TEvent>(TEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
			=> PublishAsync(typeof(TEvent), @event, options, cancellationToken);
        
    }
}
