using CloudNative.CloudEvents;

namespace Deveel.Events;

public static class EventPublisherExtensions
{
    /// <summary>
    /// Creates a <see cref="CloudEvent"/> from an annotated data object of type
    /// <typeparamref name="TEvent"/> and publishes it to the underlying channels.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type of the data object that carries the event payload and metadata.
    /// Must be decorated with <c>[Event]</c> (and optionally <c>[EventAttribute]</c>)
    /// annotations so the implementation can derive the CloudEvents attributes, or
    /// must implement <see cref="IEventConvertible"/> to provide its own
    /// <see cref="CloudEvent"/> representation.
    /// </typeparam>
    /// <param name="event">
    /// The data object that will be converted into a <see cref="CloudEvent"/>
    /// and published. Must not be <c>null</c>.
    /// </param>
    /// <param name="options">
    /// An optional <see cref="EventPublishOptions"/> instance that overrides
    /// channel-level defaults for this single call.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the event has been dispatched
    /// to all applicable channels.
    /// </returns>
    /// <exception cref="InvalidCloudEventException">
    /// Thrown when one or more required CloudEvents attributes are still absent
    /// after enrichment.
    /// </exception>
    public static Task PublishAsync<TEvent>(this IEventPublisher publisher, TEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
        where TEvent : class
        => publisher.PublishAsync(typeof(TEvent), @event, options, cancellationToken);
}