using Deveel.Data;

namespace Deveel.Events;

/// <summary>
/// Represents a repository for storing and querying event delivery records.
/// </summary>
/// <typeparam name="TRecord">
/// The type of the delivery record, which must implement <see cref="IEventDeliveryRecord"/>.
/// </typeparam>
public interface IEventDeliveryLogRepository<TRecord> : IEventPublishDeliveryLog, IRepository<TRecord>
    where TRecord : class, IEventDeliveryRecord
{
    /// <summary>
    /// Retrieves all delivery records associated with a given event identifier.
    /// </summary>
    /// <param name="eventId">The identifier of the event.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records for the specified event.
    /// </returns>
    Task<IReadOnlyList<TRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all delivery records for a given channel name.
    /// </summary>
    /// <param name="channelName">The name of the channel.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records for the specified channel.
    /// </returns>
    Task<IReadOnlyList<TRecord>> GetByChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all delivery records matching the specified outcome.
    /// </summary>
    /// <param name="outcome">The delivery outcome to filter by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records with the specified outcome.
    /// </returns>
    Task<IReadOnlyList<TRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all delivery records within a specified time range.
    /// </summary>
    /// <param name="from">The start of the time range.</param>
    /// <param name="to">The end of the time range.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records within the specified time range.
    /// </returns>
    Task<IReadOnlyList<TRecord>> GetByTimeRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>
/// A non-generic convenience interface for <see cref="IEventDeliveryLogRepository{TRecord}"/>
/// using <see cref="EventDeliveryRecord"/> as the record type.
/// </summary>
public interface IEventDeliveryLogRepository : IEventDeliveryLogRepository<EventDeliveryRecord>
{
}
