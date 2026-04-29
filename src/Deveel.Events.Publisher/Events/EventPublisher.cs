//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Collections.Concurrent;

namespace Deveel.Events
{
    /// <summary>
    /// The service that is responsible for publishing events 
    /// to the configured channels.
    /// </summary>
    public class EventPublisher
    {
        private readonly IEnumerable<IEventPublishChannel> _channels;
        private readonly ILogger _logger;
        private readonly EventPublisherPipelineDescriptor _pipelineDescriptor = new();
        private readonly object _pipelineLock = new();
        private readonly IServiceProvider _serviceProvider;

        // Cached once on first publish; the factory composes the middleware stack
        // around any terminal delegate supplied per-call.
        private Func<EventPublishDelegate, EventPublishDelegate>? _pipelineFactory;
        private readonly ConcurrentDictionary<Type, IReadOnlyList<IEventPublishChannel>> _typedChannels = new();

        /// <summary>
        /// Constructs the publisher with the given options and channels.
        /// </summary>
        /// <param name="options">The options used to configure the publisher.</param>
        /// <param name="channels">The channels used to publish events.</param>
        /// <param name="serviceProvider">
        /// The ambient <see cref="IServiceProvider"/> made available to middleware
        /// components via <see cref="EventContext.Services"/>.
        /// </param>
        /// <param name="logger">A logger used to trace publish operations.</param>
        public EventPublisher(
            IOptions<EventPublisherOptions> options,
            IEnumerable<IEventPublishChannel> channels,
            IServiceProvider serviceProvider,
            ILogger<EventPublisher>? logger = null)
        {
            PublisherOptions = options == null ? new EventPublisherOptions() : options.Value;
            _channels = channels;
            _serviceProvider = serviceProvider;
            _logger = logger ?? NullLogger<EventPublisher>.Instance;
        }

        /// <summary>
        /// Gets the service that is used to create events.
        /// </summary>
        protected IEventFactory? EventCreator => _serviceProvider.GetService<IEventFactory>();

        /// <summary>
        /// Gets the options that are used to configure the publisher.
        /// </summary>
        protected EventPublisherOptions PublisherOptions { get; }

        /// <summary>
        /// Gets the service used to obtain the current UTC time for event timestamps.
        /// When <c>null</c>, the <see cref="SetTimeStamp"/> method does not fill in
        /// the event <c>time</c> attribute automatically.
        /// </summary>
        protected IEventSystemTime? SystemTime => _serviceProvider.GetService<IEventSystemTime>();

        /// <summary>
        /// Gets the service used to generate unique identifiers for events.
        /// When <c>null</c>, the <see cref="SetEventId"/> method does not fill in
        /// the event <c>id</c> attribute automatically.
        /// </summary>
        protected IEventIdGenerator? IdGenerator => _serviceProvider.GetService<IEventIdGenerator>();

        /// <summary>
        /// Appends a middleware component of type <typeparamref name="TMiddleware"/> to the
        /// end of the publish pipeline.
        /// </summary>
        /// <typeparam name="TMiddleware">
        /// A concrete type that implements <see cref="IEventMiddleware"/>.
        /// A new instance is created for every publish call via
        /// <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities"/>,
        /// so constructor-injected services are fully supported.
        /// </typeparam>
        /// <param name="activationArguments">
        /// Optional explicit constructor arguments to pass to middleware activation.
        /// These values are provided in addition to DI-resolved services.
        /// </param>
        /// <returns>This <see cref="EventPublisher"/> instance, to allow fluent chaining.</returns>
        /// <remarks>
        /// Middleware components run in registration order.  Calling this method after
        /// the first <c>PublishAsync</c> / <c>PublishEventAsync</c> call is allowed;
        /// the pipeline is rebuilt on the next publish.
        /// </remarks>
        public EventPublisher Use<TMiddleware>(params object[] activationArguments)
            where TMiddleware : class, IEventMiddleware
        {
            lock (_pipelineLock)
            {
                _pipelineDescriptor.Add(typeof(TMiddleware), activationArguments);
                // Invalidate any cached factory so the next publish rebuilds the chain.
                _pipelineFactory = null;
            }

            return this;
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
        protected virtual CloudEvent SetTimeStamp(CloudEvent @event)
        {
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
        protected virtual CloudEvent SetSource(CloudEvent @event)
        {
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
        protected virtual CloudEvent SetEventId(CloudEvent @event)
        {
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
        protected virtual CloudEvent SetAttributes(CloudEvent @event)
        {
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
        protected virtual void ValidateCloudEvent(CloudEvent @event)
        {
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
        /// (<c>TOptions</c> from <see cref="EventPublishChannel{TOptions}"/>) and
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
        protected virtual EventPublishOptions? ResolveChannelOptions(IEventPublishChannel channel,
            EventPublishOptions? options)
        {
            if (options == null)
                return null;

            var channelType = channel.GetType();
            var expectedOptionsType = FindExpectedOptionsType(channelType);
            if (expectedOptionsType is null)
                return null;

            var channelEventType = FindChannelEventType(channelType);

            if (options is CombinedPublishOptions combined)
                return ResolveCombinedOptions(channel, combined, expectedOptionsType, channelEventType);

            return ResolveSingleOptions(channel, options, expectedOptionsType, channelEventType);
        }

        private static EventPublishOptions? ResolveCombinedOptions(
            IEventPublishChannel channel,
            CombinedPublishOptions combined,
            Type expectedOptionsType,
            Type? channelEventType)
        {
            // For combined options each bundled entry may carry its own ChannelName.
            // Only entries whose name matches the target channel (or carry no name) are eligible.
            var compatibleEntries = combined.Options.Where(o =>
                expectedOptionsType.IsInstanceOfType(o) &&
                NameMatchesChannel(o, channel));

            return channelEventType != null
                ? compatibleEntries.FirstOrDefault(o => IsTypedForEvent(o.GetType(), channelEventType))
                : compatibleEntries.FirstOrDefault(o => !IsAnyTypedOptions(o.GetType()));
        }

        private static EventPublishOptions? ResolveSingleOptions(
            IEventPublishChannel channel,
            EventPublishOptions options,
            Type expectedOptionsType,
            Type? channelEventType)
        {
            if (!expectedOptionsType.IsInstanceOfType(options) || !NameMatchesChannel(options, channel))
                return null;

            if (channelEventType != null)
                return IsTypedForEvent(options.GetType(), channelEventType) ? options : null;

            return IsAnyTypedOptions(options.GetType()) ? null : options;
        }

        private static Type? FindExpectedOptionsType(Type channelType)
        {
            for (var t = channelType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(EventPublishChannel<>))
                    return t.GetGenericArguments()[0];
            }

            return null;
        }

        private static Type? FindChannelEventType(Type channelType)
        {
            foreach (var iface in channelType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEventPublishChannel<>))
                    return iface.GetGenericArguments()[0];
            }

            return null;
        }

        /// <summary>
        /// Returns <c>true</c> when the <paramref name="options"/> carry no name filter,
        /// when the <paramref name="channel"/> does not implement
        /// <see cref="INamedEventPublishChannel"/>, or when their
        /// <see cref="INamedChannelFilter.ChannelName"/> matches the channel's
        /// <see cref="INamedEventPublishChannel.Name"/> (case-insensitive).
        /// </summary>
        private static bool NameMatchesChannel(EventPublishOptions options, IEventPublishChannel channel)
        {
            if (options is not INamedChannelFilter filter || string.IsNullOrEmpty(filter.ChannelName))
                return true;

            // Anonymous channels (non-INamedEventPublishChannel) receive everything.
            if (channel is not INamedEventPublishChannel named)
                return true;

            // A named-channel implementation with a null/empty Name is treated as unnamed.
            if (string.IsNullOrEmpty(named.Name))
                return true;

            return string.Equals(filter.ChannelName, named.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Filters <paramref name="channels"/> to those whose
        /// <see cref="INamedEventPublishChannel.Name"/> matches the
        /// <see cref="INamedChannelFilter.ChannelName"/> specified in
        /// <paramref name="options"/>.
        /// </summary>
        /// <remarks>
        /// When <paramref name="options"/> is <c>null</c>, does not implement
        /// <see cref="INamedChannelFilter"/>, or carries an empty/null channel name,
        /// all channels are returned unchanged.
        /// Channels that do not implement <see cref="INamedEventPublishChannel"/> are
        /// treated as anonymous and always pass the filter.
        /// <see cref="CombinedPublishOptions"/> intentionally does not implement
        /// <see cref="INamedChannelFilter"/>; its per-entry name filtering is handled
        /// inside <see cref="ResolveChannelOptions"/>.
        /// </remarks>
        protected virtual IEnumerable<IEventPublishChannel> FilterChannelsByName(
            IEnumerable<IEventPublishChannel> channels,
            EventPublishOptions? options)
        {
            if (options is not INamedChannelFilter { ChannelName: { Length: > 0 } name })
                return channels;

            return channels.Where(c =>
                c is not INamedEventPublishChannel named ||
                string.IsNullOrEmpty(named.Name) ||
                string.Equals(named.Name, name, StringComparison.OrdinalIgnoreCase));
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
        protected virtual Task PublishEventAsync(IEventPublishChannel channel, CloudEvent @event,
            EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var resolvedOptions = ResolveChannelOptions(channel, options);
            return channel.PublishAsync(@event, resolvedOptions, cancellationToken);
        }

        private CloudEvent EnsureEvent(CloudEvent @event)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var eventToPublish = @event;
            eventToPublish = SetEventId(eventToPublish);
            eventToPublish = SetTimeStamp(eventToPublish);
            eventToPublish = SetSource(eventToPublish);
            eventToPublish = SetAttributes(eventToPublish);

            return eventToPublish;
        }

        /// <summary>
        /// Returns a factory that, given a terminal <see cref="EventPublishDelegate"/>,
        /// returns the full middleware pipeline wrapping that terminal.
        /// The factory is built once using a double-checked lock and reused for every
        /// publish call; only the terminal (which closes over the per-call channel list)
        /// changes between calls.
        /// </summary>
        private Func<EventPublishDelegate, EventPublishDelegate> GetOrBuildPipelineFactory()
        {
            lock (_pipelineLock)
            {
                if (_pipelineFactory is not null)
                    return _pipelineFactory;

                // Start with the identity: factory(terminal) = terminal.
                Func<EventPublishDelegate, EventPublishDelegate> factory = static terminal => terminal;

                var registrations = _pipelineDescriptor.MiddlewareRegistrations;

                // Compose in reverse so the first-registered type is the outermost wrapper.
                for (var i = registrations.Count - 1; i >= 0; i--)
                {
                    var registration = registrations[i];
                    var prevFactory = factory;

                    factory = terminal =>
                    {
                        var inner = prevFactory(terminal);
                        return ctx =>
                        {
                            // A stateless middleware instance is created per invocation; its
                            // constructor dependencies are resolved from ctx.Services.
                            var mw = (IEventMiddleware)ActivatorUtilities.CreateInstance(
                                ctx.Services,
                                registration.MiddlewareType,
                                registration.ActivationArguments);
                            return mw.InvokeAsync(ctx, inner);
                        };
                    };
                }

                _logger.TraceMiddlewarePipelineBuilt(registrations.Count);
                _pipelineFactory = factory;
            }

            return _pipelineFactory;
        }

        /// <summary>
        /// The terminal step of the middleware pipeline: validates the
        /// <see cref="CloudEvent"/> and publishes it to every channel in
        /// <paramref name="channels"/>.
        /// </summary>
        private async Task DispatchToChannelsAsync(
            IReadOnlyList<IEventPublishChannel> channels,
            EventContext context)
        {
            ValidateCloudEvent(context.Event);

            foreach (var channel in channels)
            {
                _logger.TraceEventPublishing(context.Event.Type!, channel.GetType());

                try
                {
                    await PublishEventAsync(channel, context.Event, context.Options, context.CancellationToken);

                    _logger.TraceEventPublished(context.Event.Type!, channel.GetType());
                }
                catch (Exception ex)
                {
                    HandleChannelPublishError(ex, context.Event.Type!, channel.GetType());
                }
            }
        }

        private void HandleChannelPublishError(Exception ex, string eventType, Type channelType)
        {
            _logger.LogEventPublishError(ex, eventType, channelType);

            if (!PublisherOptions.ThrowOnErrors)
                return;

            throw new EventPublishException(
                $"An error occurred while publishing an event of type {eventType} to the channel '{channelType.Name}'",
                ex);
        }

        private async Task PublishEventToChannelsAsync(IEnumerable<IEventPublishChannel> channels, CloudEvent @event,
            EventPublishOptions? options = null, CancellationToken cancellationToken = default)
        {
            var targetChannels = FilterChannelsByName(channels, options).ToList();
            var eventToPublish = EnsureEvent(@event);

            var context = new EventContext(eventToPublish, _serviceProvider, cancellationToken, options);

            // The terminal closes over the channel list resolved for this specific call.
            EventPublishDelegate terminal = ctx => DispatchToChannelsAsync(targetChannels, ctx);

            var pipelineFactory = GetOrBuildPipelineFactory();
            var pipeline = pipelineFactory(terminal);

            await pipeline(context);
        }

        /// <summary>
        /// Enriches and publishes the given <see cref="CloudEvent"/> to every
        /// registered <see cref="IEventPublishChannel"/>, running the full
        /// middleware pipeline before fan-out.
        /// </summary>
        /// <param name="event">The <see cref="CloudEvent"/> to publish. Must not be <c>null</c>.</param>
        /// <param name="options">
        /// Optional per-call options that override the channel-level defaults.
        /// Pass a channel-specific sub-type (e.g. <c>RabbitMqPublishOptions</c>) to
        /// target a single channel, or a <see cref="CombinedPublishOptions"/> instance
        /// to carry heterogeneous overrides for multiple channels simultaneously.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> that completes when the event has been dispatched.</returns>
        /// <exception cref="InvalidCloudEventException">
        /// Thrown when any of the required CloudEvents attributes (<c>id</c>,
        /// <c>source</c>, <c>type</c>, <c>specversion</c>) is still absent after
        /// enrichment.
        /// </exception>
        public virtual Task PublishEventAsync(CloudEvent @event, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return PublishEventToChannelsAsync(_channels, @event, options, cancellationToken);
        }
        
        /// <summary>
        /// Creates a <see cref="CloudEvent"/> from the annotated data object of type
        /// <paramref name="eventType"/> and publishes it through the full middleware pipeline.
        /// </summary>
        /// <param name="eventType">
        /// The CLR type of the event data object. Must be decorated with
        /// <see cref="Deveel.Events.EventAttribute"/>.
        /// </param>
        /// <param name="event">
        /// The data object to convert and publish. May be <c>null</c> when the event
        /// carries no data payload.
        /// </param>
        /// <param name="options">
        /// Optional per-call options forwarded to the channel(s).
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> that completes when the event has been dispatched.</returns>
        /// <exception cref="EventPublishException">
        /// Thrown when an error occurs while creating the event from the event and
        /// <see cref="EventPublisherOptions.ThrowOnErrors"/> is <c>true</c>.
        /// </exception>
        public Task PublishAsync(Type eventType, object? @event, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryCreateCloudEvent(eventType, @event, out var cloudEvent))
                return Task.CompletedTask;

            // First let's try to resolve event publishers specific for this
            // type of object...

            var typedChannels = GetTypedChannels(eventType);
            if (typedChannels.Count > 0)
            {
                return PublishEventToChannelsAsync(typedChannels, @cloudEvent, options, cancellationToken);
            }

            return PublishEventAsync(cloudEvent, options, cancellationToken);
        }

        private bool TryCreateCloudEvent(Type eventType, object? @event, out CloudEvent cloudEvent)
        {
            try
            {
                cloudEvent = CreateEventFromData(eventType, @event);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogEventCreateError(ex, eventType);

                if (PublisherOptions.ThrowOnErrors)
                {
                    throw new EventPublishException(
                        $"An error occurred while creating an event of type {eventType.FullName} from the provided event",
                        ex);
                }

                cloudEvent = null!;
                return false;
            }
        }

        private IReadOnlyList<IEventPublishChannel> GetTypedChannels(Type dataType)
        {
            return _typedChannels.GetOrAdd(dataType, static (type, channels) =>
            {
                var channelType = typeof(IEventPublishChannel<>).MakeGenericType(type);
                return channels.Where(channelType.IsInstanceOfType).ToList();
            }, _channels);
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
        /// Thrown when the event factory is not set.
        /// </exception>
        protected virtual CloudEvent CreateEventFromData(Type dataType, object? data)
        {
            if (EventCreator == null)
                throw new NotSupportedException("Cannot create events from the data");

            return EventCreator.CreateEventFromData(dataType, data);
        }

        /// <summary>
        /// Publishes a strongly-typed data object as a <see cref="CloudEvent"/> through
        /// the full middleware pipeline.
        /// </summary>
        /// <typeparam name="TData">
        /// The type of the data to publish.  When <typeparamref name="TData"/> implements
        /// <see cref="IEventConvertible"/> the object converts itself; when it is already
        /// a <see cref="CloudEvent"/> it is published directly; otherwise
        /// <see cref="CreateEventFromData"/> is called.
        /// </typeparam>
        /// <param name="data">The data object to publish. Must not be <c>null</c>.</param>
        /// <param name="options">Optional per-call options forwarded to the channel(s).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> that completes when the event has been dispatched.</returns>
        public Task PublishAsync<TData>(TData data, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(data);

            if (data is IEventConvertible eventConvertible)
            {
                if (!TryConvertToCloudEvent(eventConvertible, typeof(TData), out var convertedEvent))
                    return Task.CompletedTask;

                return PublishEventAsync(convertedEvent, options, cancellationToken);
            }

            if (data is CloudEvent cloudEvent)
                return PublishEventAsync(cloudEvent, options, cancellationToken);

            return PublishAsync(typeof(TData), data, options, cancellationToken);
        }

        private bool TryConvertToCloudEvent(IEventConvertible convertible, Type dataType, out CloudEvent cloudEvent)
        {
            try
            {
                var converted = convertible.ToCloudEvent();
                if (converted == null)
                {
                    cloudEvent = null!;
                    return false;
                }

                cloudEvent = converted;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogEventCreateError(ex, dataType);

                if (PublisherOptions.ThrowOnErrors)
                    throw new EventPublishException("An error occurred while converting data", ex);

                cloudEvent = null!;
                return false;
            }
        }

        /// <summary>
        /// Creates a <see cref="CloudEvent"/> from an annotated data object of type
        /// <typeparamref name="TEvent"/> and publishes it only to the channel(s) whose
        /// <see cref="INamedEventPublishChannel.Name"/> matches <paramref name="channelName"/>.
        /// </summary>
        /// <typeparam name="TEvent">The event data type.</typeparam>
        /// <param name="event">The data object to publish.</param>
        /// <param name="channelName">
        /// The logical name of the target channel. Only channels registered with this
        /// name (via <see cref="INamedChannelFilter.ChannelName"/> on their options)
        /// will receive the event.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="Task"/> that completes when the event has been dispatched
        /// to all matching channels.
        /// </returns>
        public Task PublishAsync<TEvent>(TEvent @event, string channelName,
            CancellationToken cancellationToken = default)
            where TEvent : class
            => PublishAsync(typeof(TEvent), @event, new NamedChannelPublishOptions(channelName), cancellationToken);

        /// <summary>
        /// Publishes a <see cref="CloudEvent"/> only to the channel(s) whose
        /// <see cref="INamedEventPublishChannel.Name"/> matches <paramref name="channelName"/>.
        /// </summary>
        /// <param name="event">The <see cref="CloudEvent"/> to publish.</param>
        /// <param name="channelName">
        /// The logical name of the target channel(s).
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="Task"/> that completes when the event has been dispatched
        /// to all matching channels.
        /// </returns>
        public Task PublishEventAsync(CloudEvent @event, string channelName,
            CancellationToken cancellationToken = default)
            => PublishEventAsync(@event, new NamedChannelPublishOptions(channelName), cancellationToken);
    }
}