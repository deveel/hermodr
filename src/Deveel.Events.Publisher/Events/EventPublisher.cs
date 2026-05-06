//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using System.Linq;

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
    public class EventPublisher : IEventPublisher
    {
        private readonly IEnumerable<IEventPublishChannel> _channels;
        private readonly ILogger _logger;
        private readonly EventPublisherPipeline? _pipeline;
        private readonly IServiceProvider _serviceProvider;

        // Pipeline factory is built exactly once from the immutable descriptor.
        private readonly Lazy<Func<EventPublishDelegate, EventPublishDelegate>> _pipelineFactory;

        private readonly ConcurrentDictionary<Type, IReadOnlyList<IEventPublishChannel>> _typedChannels = new();
        
        /// <summary>
        /// Internal constructor used by <see cref="EventPublisherBuilder"/> to supply a
        /// pre-built, frozen <paramref name="pipeline"/> at construction time.
        /// </summary>
        public EventPublisher(
            IOptions<EventPublisherOptions> options,
            IEnumerable<IEventPublishChannel> channels,
            IServiceProvider serviceProvider,
            EventPublisherPipeline? pipeline = null,
            ILogger<EventPublisher>? logger = null)
        {
            PublisherOptions = options == null ? new EventPublisherOptions() : options.Value;
            _channels = channels;
            _serviceProvider = serviceProvider;
            _logger = logger ?? NullLogger<EventPublisher>.Instance;
            _pipeline = pipeline ?? new EventPublisherPipeline();
            _pipelineFactory = new Lazy<Func<EventPublishDelegate, EventPublishDelegate>>(
                () => _pipeline.Build(), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>Gets the service that is used to create events.</summary>
        protected IEventFactory? EventFactory => _serviceProvider.GetService<IEventFactory>();

        /// <summary>Gets the options that are used to configure the publisher.</summary>
        protected EventPublisherOptions PublisherOptions { get; }

        /// <summary>Gets the service used to obtain the current UTC time for event timestamps.</summary>
        protected IEventSystemTime? SystemTime => _serviceProvider.GetService<IEventSystemTime>();

        /// <summary>Gets the service used to generate unique identifiers for events.</summary>
        protected IEventIdGenerator? IdGenerator => _serviceProvider.GetService<IEventIdGenerator>();

        /// <summary>Sets the timestamp of the event if it was not already set.</summary>
        protected virtual CloudEvent SetTimeStamp(CloudEvent @event)
        {
            if (@event.Time == null && SystemTime != null)
                @event.Time = SystemTime.UtcNow;
            return @event;
        }

        /// <summary>Sets the source of the event if it was not already set.</summary>
        protected virtual CloudEvent SetSource(CloudEvent @event)
        {
            if (@event.Source == null && PublisherOptions.Source != null)
                @event.Source = PublisherOptions.Source;
            return @event;
        }

        /// <summary>Sets the identifier of the event if it was not already set.</summary>
        protected virtual CloudEvent SetEventId(CloudEvent @event)
        {
            if (@event.Id == null && IdGenerator != null)
                @event.Id = IdGenerator.GenerateId();
            return @event;
        }

        /// <summary>Adds the attributes configured for the publisher into the event.</summary>
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
        /// attributes after enrichment and before dispatch to any channel.
        /// </summary>
        protected virtual void ValidateCloudEvent(CloudEvent @event)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(@event.Id)) missing.Add("id");
            if (@event.Source == null) missing.Add("source");
            if (string.IsNullOrWhiteSpace(@event.Type)) missing.Add("type");
            if (string.IsNullOrWhiteSpace(@event.SpecVersion?.VersionId)) missing.Add("specversion");
            if (missing.Count > 0) throw new InvalidCloudEventException(missing);
        }

        /// <summary>Resolves the options to pass to the given channel.</summary>
        protected virtual EventPublishOptions? ResolveChannelOptions(IEventPublishChannel channel,
            EventPublishOptions? options)
        {
            // Peel off any transport wrapper so channel resolution always works
            // against the effective channel-specific options.
            options = options?.Unwrap();

            if (options == null) return null;
            var effectiveChannel = UnwrapDecoratedChannel(channel);
            var channelType = effectiveChannel.GetType();
            var expectedOptionsType = FindExpectedOptionsType(channelType);
            if (expectedOptionsType is null) return null;
            var channelEventType = FindChannelEventType(channelType);
            if (options is CombinedPublishOptions combined)
                return ResolveCombinedOptions(channel, combined, expectedOptionsType, channelEventType);
            return ResolveSingleOptions(channel, options, expectedOptionsType, channelEventType);
        }

        private static IEventPublishChannel UnwrapDecoratedChannel(IEventPublishChannel channel)
        {
            while (channel is IEventPublishChannelDecorator decorator)
                channel = decorator.InnerChannel;

            return channel;
        }

        private static EventPublishOptions? ResolveCombinedOptions(
            IEventPublishChannel channel,
            CombinedPublishOptions combined,
            Type expectedOptionsType,
            Type? channelEventType)
        {
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
            foreach (var iface in channelType.GetInterfaces()
                .Where(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEventPublishChannel<>)))
            {
                return iface.GetGenericArguments()[0];
            }
            return null;
        }

        private static bool NameMatchesChannel(EventPublishOptions options, IEventPublishChannel channel)
        {
            if (options is not INamedChannelFilter filter || string.IsNullOrEmpty(filter.ChannelName))
                return true;
            if (channel is not INamedEventPublishChannel named)
                return true;
            if (string.IsNullOrEmpty(named.Name))
                return true;
            return string.Equals(filter.ChannelName, named.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Filters channels by name according to the options.</summary>
        protected virtual IEnumerable<IEventPublishChannel> FilterChannelsByName(
            IEnumerable<IEventPublishChannel> channels,
            EventPublishOptions? options)
        {
            // Peel off any transport wrapper before applying the named-channel filter.
            var effectiveOptions = options?.Unwrap();

            if (effectiveOptions is not INamedChannelFilter { ChannelName: { Length: > 0 } name })
                return channels;
            return channels.Where(c =>
                c is not INamedEventPublishChannel named ||
                string.IsNullOrEmpty(named.Name) ||
                string.Equals(named.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTypedForEvent(Type optionsType, Type eventType)
        {
            for (var t = optionsType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericArguments().Any(arg => arg == eventType))
                    return true;
            }
            return false;
        }

        private static bool IsAnyTypedOptions(Type optionsType)
        {
            for (var t = optionsType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType) return true;
            }
            return false;
        }

        /// <summary>Publishes the event to a single channel, resolving per-call options first.</summary>
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


        private async Task DispatchToChannelsAsync(
            IReadOnlyList<IEventPublishChannel> channels,
            EventContext context)
        {
            ValidateCloudEvent(context.Event);
            foreach (var channel in channels)
            {
                var effectiveChannel = UnwrapDecoratedChannel(channel);
                _logger.TraceEventPublishing(context.Event.Type!, effectiveChannel.GetType());
                try
                {
                    await PublishEventAsync(channel, context.Event, context.Options, context.CancellationToken);
                    _logger.TraceEventPublished(context.Event.Type!, effectiveChannel.GetType());
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    await HandlePublishErrorAsync(new EventPublishErrorContext(
                        PublisherOptions.PublisherName,
                        EventPublishStage.ChannelPublish,
                        ex,
                        context.Services,
                        context.CancellationToken,
                        context.Event,
                        context.Options,
                        context.RawOptions,
                        effectiveChannel.GetType(),
                        (channel as INamedEventPublishChannel)?.Name));
                    HandleChannelPublishError(ex, context.Event.Type!, effectiveChannel.GetType());
                }
            }
        }

        private void HandleChannelPublishError(Exception ex, string eventType, Type channelType)
        {
            _logger.LogEventPublishError(ex, eventType, channelType);
            if (!PublisherOptions.ThrowOnErrors) return;
            throw new EventPublishChannelException(
                $"An error occurred while publishing an event of type {eventType} to the channel '{channelType.Name}'",
                ex);
        }

        private IEnumerable<IEventPublishErrorHandler> GetPublishErrorHandlers(IServiceProvider services)
        {
            var publisherName = PublisherOptions.PublisherName ?? String.Empty;
            return services.GetKeyedServices<IEventPublishErrorHandler>(publisherName);
        }

        private async Task HandlePublishErrorAsync(EventPublishErrorContext context)
        {
            var handlers = GetPublishErrorHandlers(context.Services).ToList();
            if (handlers.Count == 0)
                return;

            foreach (var handler in handlers)
            {
                try
                {
                    await handler.HandleAsync(context);
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception handlerException)
                {
                    _logger.LogPublishErrorHandlerError(handlerException, context.Stage, context.Event?.Type, context.ChannelType);
                    throw new EventPublishErrorHandlerException(
                        $"An error occurred while handling a publish error at stage {context.Stage}",
                        new AggregateException(context.Exception, handlerException));
                }
            }
        }

        private async Task PublishEventToChannelsAsync(IEnumerable<IEventPublishChannel> channels, CloudEvent @event,
            EventPublishOptions? options = null, CancellationToken cancellationToken = default)
        {
            var targetChannels = FilterChannelsByName(channels, options).ToList();
            await using var scope = _serviceProvider.CreateAsyncScope();

            // The bypass wrapper is transport-only: peel it off so the EventContext
            // (and any middleware that inspects context.Options) sees the real options.
            var bypassPipeline = options is BypassPipelinePublishOptions;
            var effectiveOptions = options?.Unwrap();

            var context = new EventContext(@event, scope.ServiceProvider, cancellationToken, effectiveOptions, options);
            EventPublishDelegate terminal = ctx =>
            {
                ctx.Event = EnsureEvent(ctx.Event);
                return DispatchToChannelsAsync(targetChannels, ctx);
            };

            var pipeline = bypassPipeline ? terminal : _pipelineFactory.Value(terminal);

            if (bypassPipeline)
                _logger.TracePipelineExecuting(@event.Type + " [pipeline bypassed]");
            else
                _logger.TracePipelineExecuting(@event.Type);

            await pipeline(context);
            _logger.TracePipelineCompleted(@event.Type);
        }

        /// <inheritdoc/>
        public virtual Task PublishEventAsync(CloudEvent @event, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
            => PublishEventToChannelsAsync(_channels, @event, options, cancellationToken);

        /// <inheritdoc/>
        public async Task PublishAsync(Type eventType, object? @event, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var cloudEvent = await TryCreateCloudEventAsync(eventType, @event, options, cancellationToken);
            if (cloudEvent == null)
                return;

            var typedChannels = GetTypedChannels(eventType);
            if (typedChannels.Count > 0)
            {
                await PublishEventToChannelsAsync(typedChannels, cloudEvent, options, cancellationToken);
                return;
            }

            await PublishEventAsync(cloudEvent, options, cancellationToken);
        }

        private async Task<CloudEvent?> TryCreateCloudEventAsync(
            Type eventType,
            object? @event,
            EventPublishOptions? options,
            CancellationToken cancellationToken)
        {
            try
            {
                return CreateEventFromData(eventType, @event);
            }
            catch (Exception ex)
            {
                _logger.LogEventCreateError(ex, eventType);
                await using var scope = _serviceProvider.CreateAsyncScope();
                await HandlePublishErrorAsync(new EventPublishErrorContext(
                    PublisherOptions.PublisherName,
                    EventPublishStage.EventCreation,
                    ex,
                    scope.ServiceProvider,
                    cancellationToken,
                    options: options?.Unwrap(),
                    rawOptions: options,
                    dataType: eventType,
                    data: @event));
                if (PublisherOptions.ThrowOnErrors)
                    throw new EventCreationException(
                        $"An error occurred while creating an event of type {eventType.FullName} from the provided event",
                        ex);
                return null;
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

        /// <summary>Creates an event from the given data type and the instance of the data.</summary>
        protected virtual CloudEvent CreateEventFromData(Type dataType, object? data)
        {
            if (EventFactory == null)
                throw new NotSupportedException("Cannot create events from the data");
            return EventFactory.CreateEventFromData(dataType, data);
        }
        
        /// <inheritdoc cref="IEventPublisher.PublishAsync(Type,object,EventPublishOptions,CancellationToken)"/>
        /// <typeparam name="TEvent">
        /// The compile-time type of the event data object.  When
        /// <typeparamref name="TEvent"/> implements <see cref="IEventConvertible"/> the
        /// event is converted via <see cref="IEventConvertible.ToCloudEvent"/> rather
        /// than through the <see cref="IEventFactory"/> pipeline.
        /// </typeparam>
        /// <param name="event">The event data or <see cref="CloudEvent"/> to publish.</param>
        /// <param name="options">Optional per-call options forwarded to the channel(s).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task PublishAsync<TEvent>(TEvent @event, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            if (@event is IEventConvertible eventConvertible)
            {
                var convertedEvent = await TryConvertToCloudEventAsync(
                    eventConvertible,
                    typeof(TEvent),
                    @event,
                    options,
                    cancellationToken);
                if (convertedEvent == null)
                    return;

                await PublishEventAsync(convertedEvent, options, cancellationToken);
                return;
            }

            if (@event is CloudEvent cloudEvent)
            {
                await PublishEventAsync(cloudEvent, options, cancellationToken);
                return;
            }

            await PublishAsync(typeof(TEvent), @event, options, cancellationToken);
        }

        private async Task<CloudEvent?> TryConvertToCloudEventAsync(
            IEventConvertible convertible,
            Type dataType,
            object? data,
            EventPublishOptions? options,
            CancellationToken cancellationToken)
        {
            try
            {
                var converted = convertible.ToCloudEvent();
                if (converted == null)
                {
                    return null;
                }

                return converted;
            }
            catch (Exception ex)
            {
                _logger.LogEventCreateError(ex, dataType);
                await using var scope = _serviceProvider.CreateAsyncScope();
                await HandlePublishErrorAsync(new EventPublishErrorContext(
                    PublisherOptions.PublisherName,
                    EventPublishStage.EventConversion,
                    ex,
                    scope.ServiceProvider,
                    cancellationToken,
                    options: options?.Unwrap(),
                    rawOptions: options,
                    dataType: dataType,
                    data: data));
                if (PublisherOptions.ThrowOnErrors)
                    throw new EventConversionException("An error occurred while converting data", ex);
                return null;
            }
        }
    }
}
