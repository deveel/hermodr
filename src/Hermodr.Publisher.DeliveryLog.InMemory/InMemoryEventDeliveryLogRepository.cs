using Kista;

namespace Hermodr;

/// <summary>
/// An in-memory repository for event delivery log records.
/// </summary>
public class InMemoryEventDeliveryLogRepository : InMemoryRepository<EventDeliveryRecord>, IEventPublishDeliveryLog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryEventDeliveryLogRepository"/> class.
    /// </summary>
    /// <param name="list">An optional initial collection of delivery records.</param>
    public InMemoryEventDeliveryLogRepository(IEnumerable<EventDeliveryRecord>? list = null)
        : base(list)
    {
    }

    /// <inheritdoc />
    public string ProviderName => "InMemory";

    /// <inheritdoc />
    public ValueTask RecordAsync(EventDeliveryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return base.AddAsync(record, cancellationToken);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<IReadOnlyList<EventDeliveryRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken cancellationToken = default)
    {
        var results = Entities
            .Where(r => r.Outcome == outcome)
            .OrderByDescending(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    /// <inheritdoc />
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
