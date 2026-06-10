using Kista;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hermodr;

public class EntityEventDeliveryLogRepository : EntityRepository<DbEventDeliveryRecord, string>, IEventPublishDeliveryLog
{
    private readonly IEventSystemTime _systemTime;
    private readonly IEventDeliveryRecordFactory<DbEventDeliveryRecord> _recordFactory;

    public EntityEventDeliveryLogRepository(
        DeliveryLogDbContext context,
        IEventDeliveryRecordFactory<DbEventDeliveryRecord> recordFactory,
        IEventSystemTime? systemTime = null,
        IServiceProvider? services = null,
        ILogger<EntityRepository<DbEventDeliveryRecord, string>>? logger = null)
        : base(context, services!, logger!)
    {
        _recordFactory = recordFactory;
        _systemTime = systemTime ?? EventSystemTime.Instance;
    }

    public string ProviderName => "EntityFrameworkCore";

    public async ValueTask RecordAsync(EventDeliveryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entity = _recordFactory.CreateRecord(record);
        await AddAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<DbEventDeliveryRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        return await Entities
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DbEventDeliveryRecord>> GetByChannelAsync(string channelName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        return await Entities
            .Where(r => r.ChannelName != null && r.ChannelName == channelName)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DbEventDeliveryRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken cancellationToken = default)
    {
        var outcomeStr = outcome.ToString();

        return await Entities
            .Where(r => r.Outcome == outcomeStr)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DbEventDeliveryRecord>> GetByTimeRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        return await Entities
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(cancellationToken);
    }
}
