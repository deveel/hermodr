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
public abstract class EventPublishChannelOptions
{
}