namespace Deveel.Events;

/// <summary>
/// Base class for options that configure a specific <see cref="IEventPublishChannel"/>
/// implementation.
/// </summary>
/// <remarks>
/// Channel-specific options classes inherit from this type and add their own
/// properties (connection strings, endpoint URLs, serialization settings, etc.).
/// An options instance can be used in two ways:
/// <list type="bullet">
///   <item>
///     <description>
///       As the <strong>channel-level default</strong> — resolved from the DI container
///       via <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> and injected
///       into the channel constructor.
///     </description>
///   </item>
///   <item>
///     <description>
///       As a <strong>per-call override</strong> — passed directly to
///       <see cref="IEventPublishChannel.PublishAsync"/> to override individual settings
///       for a single delivery without changing the channel-level defaults.
///     </description>
///   </item>
/// </list>
/// </remarks>
public abstract class EventPublishOptions
{
    /// <summary>
    /// Gets or sets the UTC time when event delivery should be attempted.
    /// When <c>null</c>, delivery is attempted immediately.
    /// </summary>
    public DateTimeOffset? ScheduleDeliveryAt { get; set; }


    /// <summary>
    /// Creates a <see cref="CombinedPublishOptions"/> that bundles multiple per-channel
    /// options instances together so that a single call to
    /// <see cref="EventPublisher.PublishEventAsync(CloudNative.CloudEvents.CloudEvent, EventPublishOptions, System.Threading.CancellationToken)"/> or
    /// <see cref="EventPublisher.PublishAsync(System.Type, object, EventPublishOptions, System.Threading.CancellationToken)"/> can carry heterogeneous overrides for
    /// several channels simultaneously.
    /// </summary>
    /// <param name="options">
    /// The per-channel options to bundle. The order of the elements is preserved; the
    /// first compatible entry wins when <see cref="EventPublisher"/> resolves options for
    /// a specific channel. Each entry may implement <see cref="INamedChannelFilter"/> to
    /// restrict itself to a channel whose <see cref="INamedEventPublishChannel.Name"/>
    /// matches <see cref="INamedChannelFilter.ChannelName"/>.
    /// </param>
    /// <returns>
    /// A new <see cref="CombinedPublishOptions"/> containing all supplied entries.
    /// </returns>
    /// <example>
    /// <code language="csharp">
    /// var combined = EventPublishOptions.Combine(
    ///     new RabbitMqPublishOptions { ChannelName = "rabbit-orders",  RoutingKey = "orders" },
    ///     new ServiceBusPublishOptions { ChannelName = "sb-audit", QueueName  = "audit" });
    ///
    /// await publisher.PublishEventAsync(myEvent, combined);
    /// </code>
    /// </example>
    public static CombinedPublishOptions Combine(params EventPublishOptions[] options)
        => new CombinedPublishOptions(options);

    /// <summary>
    /// Creates an <see cref="EventPublishOptions"/> instance that signals to the
    /// <see cref="EventPublisher"/> that the middleware pipeline should be skipped
    /// entirely for the publish call, dispatching the event directly to the target
    /// channels.
    /// </summary>
    /// <param name="innerOptions">
    /// Optional channel-specific options to forward to channel selection and per-channel
    /// option resolution.  Use this when you need to target a specific channel (e.g. by
    /// name or type) while still bypassing the middleware pipeline.  When <c>null</c>
    /// every registered channel receives the event and each channel's defaults are used.
    /// </param>
    /// <returns>
    /// An <see cref="EventPublishOptions"/> that carries the bypass signal and wraps
    /// any supplied <paramref name="innerOptions"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is intended for components that are themselves invoked from within
    /// the publish pipeline (such as a routing subscription or an outbox relay processor)
    /// and that need to re-publish an event without triggering the full middleware stack
    /// again.  Running the pipeline a second time would cause re-entrant behaviour such
    /// as duplicate subscription dispatches or, in the outbox case, an infinite
    /// persist-and-relay loop.
    /// </para>
    /// <para>
    /// Example — route to a named channel while bypassing the pipeline:
    /// <code language="csharp">
    /// var options = EventPublishOptions.BypassPipeline(
    ///     new RabbitMqPublishOptions { ChannelName = "rabbit-orders", RoutingKey = "orders" });
    ///
    /// await publisher.PublishEventAsync(myEvent, options);
    /// </code>
    /// </para>
    /// </remarks>
    public static EventPublishOptions BypassPipeline(EventPublishOptions? innerOptions = null)
        => new BypassPipelinePublishOptions(innerOptions);

    /// <summary>
    /// Returns the effective options after stripping any transport-only wrappers
    /// (such as the internal bypass-pipeline signal).
    /// </summary>
    /// <returns>
    /// For most subclasses this is <c>this</c>.  When the instance is a bypass
    /// transport wrapper it returns the wrapped inner options, or <c>null</c> when
    /// no inner options were supplied.
    /// </returns>
    /// <remarks>
    /// Middleware authors and test helpers should use this method rather than
    /// inspecting the concrete type of the options object, so that transport wrappers
    /// remain an implementation detail of the publisher infrastructure.
    /// </remarks>
    public virtual EventPublishOptions? Unwrap() => this;
}