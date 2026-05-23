using Deveel.Data;

namespace Deveel.Events;

/// <summary>
/// An in-memory implementation of <see cref="IEventDeliveryLogRepository"/> that stores
/// delivery records in a volatile collection.
/// </summary>
public class InMemoryEventDeliveryLogRepository : InMemoryRepository<EventDeliveryRecord>, IEventDeliveryLogRepository
{
    /// <summary>
    /// Creates a new instance of <see cref="InMemoryEventDeliveryLogRepository"/>,
    /// optionally seeded with an initial list of records.
    /// </summary>
    /// <param name="list">
    /// An optional initial list of delivery records to populate the repository.
    /// </param>
    public InMemoryEventDeliveryLogRepository(IEnumerable<EventDeliveryRecord>? list = null)
        : base(list)
    {
    }

    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    public string ProviderName => "InMemory";

    /// <summary>
    /// Records a delivery attempt in the in-memory store.
    /// </summary>
    /// <param name="record">
    /// The record of the event delivery to be stored.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    public Task RecordAsync(IEventDeliveryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        
        var toAdd = EventDeliveryRecord.FromRecord(record);
        return AddAsync(toAdd, cancellationToken);
    }

    /// <summary>
    /// Retrieves all delivery records associated with the given event identifier.
    /// </summary>
    /// <param name="eventId">The identifier of the event.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records for the specified event.
    /// </returns>
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        var results = Entities
            .Where(r => string.Equals(r.Event?.Id, eventId, StringComparison.Ordinal))
            .OrderBy(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <summary>
    /// Retrieves all delivery records for the given channel name.
    /// </summary>
    /// <param name="channelName">The name of the channel.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records for the specified channel.
    /// </returns>
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByChannelAsync(string channelName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        var results = Entities
            .Where(r => string.Equals(r.ChannelName, channelName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <summary>
    /// Retrieves all delivery records matching the specified outcome.
    /// </summary>
    /// <param name="outcome">The delivery outcome to filter by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records with the specified outcome.
    /// </returns>
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken cancellationToken = default)
    {
        var results = Entities
            .Where(r => r.Outcome == outcome)
            .OrderByDescending(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <summary>
    /// Retrieves all delivery records within the specified time range.
    /// </summary>
    /// <param name="from">The start of the time range.</param>
    /// <param name="to">The end of the time range.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records within the specified time range.
    /// </returns>
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByTimeRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var results = Entities
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }
}
