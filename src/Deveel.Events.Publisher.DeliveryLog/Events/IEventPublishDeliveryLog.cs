namespace Deveel.Events;

/// <summary>
/// Provides a service to record the delivery of events to channels.
/// </summary>
public interface IEventPublishDeliveryLog
{
    /// <summary>
    /// Records the delivery of an event to a channel.
    /// </summary>
    /// <param name="record">
    /// The record of the event delivery to be stored.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    Task RecordAsync(IEventDeliveryRecord record, CancellationToken cancellationToken = default);
}
