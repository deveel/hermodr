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
    /// Creates a <see cref="CombinedPublishOptions"/> that bundles multiple per-channel
    /// options instances together so that a single call to
    /// <see cref="IEventPublisher.PublishEventAsync"/> or
    /// <see cref="IEventPublisher.PublishAsync"/> can carry heterogeneous overrides for
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
}