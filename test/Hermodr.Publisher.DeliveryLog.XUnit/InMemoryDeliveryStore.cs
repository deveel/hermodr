using System.Collections.Concurrent;

namespace Hermodr;

public class InMemoryDeliveryStore : IEventPublishDeliveryLog
{
    private readonly ConcurrentDictionary<string, EventDeliveryRecord> _records = new();

    public InMemoryDeliveryStore(IEnumerable<EventDeliveryRecord>? list = null)
    {
        if (list != null)
        {
            foreach (var r in list)
                _records.TryAdd(r.Id, r);
        }
    }

    public string ProviderName => "InMemory";

    public ValueTask RecordAsync(EventDeliveryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.Id] = record;
        return ValueTask.CompletedTask;
    }

    public Task<IReadOnlyList<EventDeliveryRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var results = _records.Values
            .Where(r => string.Equals(r.Event?.Id, eventId, StringComparison.Ordinal))
            .OrderBy(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    public Task<IReadOnlyList<EventDeliveryRecord>> GetByChannelAsync(string channelName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        var results = _records.Values
            .Where(r => string.Equals(r.ChannelName, channelName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    public Task<IReadOnlyList<EventDeliveryRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken cancellationToken = default)
    {
        var results = _records.Values
            .Where(r => r.Outcome == outcome)
            .OrderByDescending(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }

    public Task<IReadOnlyList<EventDeliveryRecord>> GetByTimeRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var results = _records.Values
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<EventDeliveryRecord>>(results);
    }
}
