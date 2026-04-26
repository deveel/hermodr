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
		private IDictionary<Type, IReadOnlyList<IEventPublishChannel>>? _typedChannels;

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

    /// <summary>
    /// Gets the service used to obtain the current UTC time for event timestamps.
    /// When <c>null</c>, the <see cref="SetTimeStamp"/> method does not fill in
    /// the event <c>time</c> attribute automatically.
    /// </summary>
    protected IEventSystemTime? SystemTime { get; }

    /// <summary>
    /// Gets the service used to generate unique identifiers for events.
    /// When <c>null</c>, the <see cref="SetEventId"/> method does not fill in
    /// the event <c>id</c> attribute automatically.
    /// </summary>
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
		/// Resolves the options to pass to the given channel.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The method first determines the channel's declared options type
		/// (<c>TOptions</c> from <see cref="EventPublishChannelBase{TOptions}"/>) and
		/// whether the channel is a <em>typed</em> channel (i.e. implements
		/// <see cref="IEventPublishChannel{TEvent}"/> for some <c>TEvent</c>).
		/// </para>
		/// <para>
		/// Matching rules for per-call <paramref name="options"/>:
		/// <list type="bullet">
		///   <item>
		///     <description>
		///       <strong>Typed channel</strong> (<c>IEventPublishChannel&lt;TEvent&gt;</c>) —
		///       only accepts options whose runtime type (or an ancestor in its hierarchy) is a
		///       closed generic type parameterised with <c>TEvent</c>
		///       (e.g. <c>RabbitMqPublishOptions&lt;OrderPlaced&gt;</c>).
		///       A non-generic options instance (e.g. bare <c>RabbitMqPublishOptions</c>)
		///       is <strong>not</strong> forwarded; the channel falls back to its registered
		///       defaults instead.
		///     </description>
		///   </item>
		///   <item>
		///     <description>
		///       <strong>General channel</strong> (no <c>IEventPublishChannel&lt;TEvent&gt;</c>) —
		///       only accepts options whose runtime type (and every ancestor) is
		///       a non-generic type.  Typed options (e.g.
		///       <c>RabbitMqPublishOptions&lt;OrderPlaced&gt;</c>) are
		///       <strong>not</strong> forwarded to general channels.
		///     </description>
		///   </item>
		/// </list>
		/// </para>
		/// <para>
		/// When <paramref name="options"/> is a <see cref="CombinedPublishOptions"/> the same
		/// rules are applied to each bundled entry; the first compatible entry wins.
		/// </para>
		/// </remarks>
		/// <param name="channel">The target channel.</param>
		/// <param name="options">The caller-supplied per-call options, or <c>null</c>.</param>
		/// <returns>
		/// Compatible options for the channel, or <c>null</c> to use the channel defaults.
		/// </returns>
		protected virtual EventPublishOptions? ResolveChannelOptions(IEventPublishChannel channel, EventPublishOptions? options)
		{
			if (options == null)
				return null;

			// Walk the inheritance chain looking for EventPublishChannelBase<TOptions>.
			var channelType = channel.GetType();
			Type? expectedOptionsType = null;
			for (var t = channelType; t != null && t != typeof(object); t = t.BaseType)
			{
				if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(EventPublishChannelBase<>))
				{
					expectedOptionsType = t.GetGenericArguments()[0];
					break;
				}
			}

			// Channel does not derive from EventPublishChannelBase<TOptions> – pass null.
			if (expectedOptionsType == null)
				return null;

			// Determine whether this is a typed channel (IEventPublishChannel<TEvent>).
			Type? channelEventType = null;
			foreach (var iface in channelType.GetInterfaces())
			{
				if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEventPublishChannel<>))
				{
					channelEventType = iface.GetGenericArguments()[0];
					break;
				}
			}

			if (options is CombinedPublishOptions combined)
			{
				return channelEventType != null
					// Typed channel: find the first bundled entry typed for this TEvent.
					? combined.Options.FirstOrDefault(o =>
						expectedOptionsType.IsAssignableFrom(o.GetType()) &&
						IsTypedForEvent(o.GetType(), channelEventType))
					// General channel: find the first non-typed bundled entry.
					: combined.Options.FirstOrDefault(o =>
						expectedOptionsType.IsAssignableFrom(o.GetType()) &&
						!IsAnyTypedOptions(o.GetType()));
			}

			// Single options instance.
			if (!expectedOptionsType.IsAssignableFrom(options.GetType()))
				return null;

			if (channelEventType != null)
				// Typed channel: only accept options specifically typed for this TEvent.
				return IsTypedForEvent(options.GetType(), channelEventType) ? options : null;

			// General channel: only accept non-typed options instances.
			return IsAnyTypedOptions(options.GetType()) ? null : options;
		}

		/// <summary>
		/// Returns <c>true</c> when <paramref name="optionsType"/> or any type in its
		/// inheritance chain is a closed generic type that carries <paramref name="eventType"/>
		/// as one of its type arguments.
		/// </summary>
		private static bool IsTypedForEvent(Type optionsType, Type eventType)
		{
			for (var t = optionsType; t != null && t != typeof(object); t = t.BaseType)
			{
				if (t.IsGenericType && t.GetGenericArguments().Any(arg => arg == eventType))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Returns <c>true</c> when <paramref name="optionsType"/> or any type in its
		/// inheritance chain is a generic type (indicating typed-channel options such as
		/// <c>RabbitMqPublishOptions&lt;TEvent&gt;</c>).
		/// </summary>
		private static bool IsAnyTypedOptions(Type optionsType)
		{
			for (var t = optionsType; t != null && t != typeof(object); t = t.BaseType)
			{
				if (t.IsGenericType)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Publishes the event to the given channel, resolving per-call options first.
		/// </summary>
		/// <param name="channel">The instance of the channel to publish to.</param>
		/// <param name="event">The event to publish.</param>
		/// <param name="options">Optional per-call options; incompatible types are resolved to <c>null</c>.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		protected virtual Task PublishEventAsync(IEventPublishChannel channel, CloudEvent @event, EventPublishOptions? options = null,
			CancellationToken cancellationToken = default)
		{
			var resolvedOptions = ResolveChannelOptions(channel, options);
			return channel.PublishAsync(@event, resolvedOptions, cancellationToken);
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

		private async Task PublishEventToChannelsAsync(IEnumerable<IEventPublishChannel> channels, CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
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
		public virtual Task PublishEventAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
		{
			return PublishEventToChannelsAsync(_channels, @event, options, cancellationToken);
		}

		/// <inheritdoc cref="IEventPublisher.PublishAsync(Type,object,EventPublishOptions,CancellationToken)"/>
		/// <exception cref="EventPublishException">
		/// Thrown when an error occurs while creating the event from the event and
		/// <see cref="EventPublisherOptions.ThrowOnErrors"/> is <c>true</c>.
		/// </exception>
		public Task PublishAsync(Type eventType, object? @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
		{
			CloudEvent cloudEvent;

			try
			{
				cloudEvent = CreateEventFromData(eventType, @event);
			}
			catch (Exception ex)
			{
				_logger.LogEventCreateError(ex, eventType);

				if (PublisherOptions.ThrowOnErrors)
					throw new EventPublishException(
						$"An error occurred while creating an event of type {eventType.FullName} from the provided event",
						ex);

				return Task.CompletedTask;
			}

			// First let's try to resolve event publishers specific for this
			// type of object...
			
			var typedChannels = GetTypedChannels(eventType);
			if (typedChannels.Count > 0)
			{
				return PublishEventToChannelsAsync(typedChannels, @cloudEvent, options, cancellationToken);
			}

			return PublishEventAsync(cloudEvent, options, cancellationToken);
		}

		private IReadOnlyList<IEventPublishChannel> GetTypedChannels(Type dataType)
		{
			if (_typedChannels == null)
				_typedChannels = new Dictionary<Type, IReadOnlyList<IEventPublishChannel>>();
			
			if (!_typedChannels.TryGetValue(dataType, out var typedChannels))
			{
				var channelType = typeof(IEventPublishChannel<>).MakeGenericType(dataType);
				typedChannels = _channels.Where(channelType.IsInstanceOfType).ToList();
				_typedChannels[dataType] = typedChannels;
			}
			
			return typedChannels;
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

		/// <inheritdoc cref="IEventPublisher.PublishAsync{TData}(TData,EventPublishOptions,CancellationToken)"/>
		public Task PublishAsync<TData>(TData data, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(data);

			if (data is IEventConvertible eventConvertible)
			{
				CloudEvent? @event = null;
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
