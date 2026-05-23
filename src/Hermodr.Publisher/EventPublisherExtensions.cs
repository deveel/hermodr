using CloudNative.CloudEvents;

namespace Hermodr;

/// <summary>
/// Extension methods that add convenience overloads to <see cref="IEventPublisher"/>.
/// </summary>
public static class EventPublisherExtensions
{
    /// <summary>
    /// Publishes a strongly-typed event object as a <see cref="CloudEvent"/> through
    /// the full middleware pipeline.
    /// </summary>
    /// <typeparam name="TEvent">The compile-time type of the event data object.</typeparam>
    /// <param name="publisher">The publisher to use.</param>
    /// <param name="event">The event object to publish. Must not be <c>null</c>.</param>
    /// <param name="options">Optional per-call options forwarded to the channel(s).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static Task PublishAsync<TEvent>(this IEventPublisher publisher, TEvent @event, EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    => publisher.PublishAsync(typeof(TEvent), @event, options, cancellationToken);

    /// <summary>
    /// Creates a <see cref="CloudEvent"/> from an annotated data object and publishes
    /// it only to the channel(s) whose name matches <paramref name="channelName"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event data type.</typeparam>
    /// <param name="publisher">The publisher to use.</param>
    /// <param name="event">The data object to publish.</param>
    /// <param name="channelName">The logical name of the target channel.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static Task PublishAsync<TEvent>(this IEventPublisher publisher, TEvent @event, string channelName,
        CancellationToken cancellationToken = default)
        where TEvent : class
    => publisher.PublishAsync(typeof(TEvent), @event, new NamedChannelPublishOptions(channelName), cancellationToken);

    /// <summary>
    /// Publishes a <see cref="CloudEvent"/> only to the channel(s) whose name
    /// matches <paramref name="channelName"/>.
    /// </summary>
    /// <param name="publisher">The publisher to use.</param>
    /// <param name="event">The <see cref="CloudEvent"/> to publish.</param>
    /// <param name="channelName">The logical name of the target channel(s).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static Task PublishEventAsync(this IEventPublisher publisher, CloudEvent @event, string channelName,
        CancellationToken cancellationToken = default)
    => publisher.PublishEventAsync(@event, new NamedChannelPublishOptions(channelName), cancellationToken);

}