using Kista;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr;

/// <summary>
/// An Entity Framework-backed repository for event delivery log records.
/// </summary>
public class EntityEventDeliveryLogRepository : EntityRepository<DbEventDeliveryRecord, string>, IEventPublishDeliveryLog
{
    private readonly IEventDeliveryRecordFactory<DbEventDeliveryRecord> _recordFactory;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityEventDeliveryLogRepository"/> class.
    /// </summary>
    /// <param name="context">The <see cref="DeliveryLogDbContext"/> to use for data access.</param>
    /// <param name="recordFactory">The factory used to create <see cref="DbEventDeliveryRecord"/> instances.</param>
    /// <param name="serviceProvider">The service provider for lazy resolution of dependencies.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public EntityEventDeliveryLogRepository(
        DeliveryLogDbContext context,
        IEventDeliveryRecordFactory<DbEventDeliveryRecord> recordFactory,
        IServiceProvider serviceProvider,
        ILogger<EntityRepository<DbEventDeliveryRecord, string>>? logger = null)
        : base(context, serviceProvider, logger!)
    {
        _recordFactory = recordFactory;
        _serviceProvider = serviceProvider;
    }

    private IEventSystemTime SystemTime => _serviceProvider.GetRequiredService<IEventSystemTime>();

    /// <inheritdoc />
    public string ProviderName => "EntityFrameworkCore";

    /// <inheritdoc />
    public async ValueTask RecordAsync(EventDeliveryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entity = _recordFactory.CreateRecord(record);
        await AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DbEventDeliveryRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        return await Entities
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DbEventDeliveryRecord>> GetByChannelAsync(string channelName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        return await Entities
            .Where(r => r.ChannelName != null && r.ChannelName == channelName)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DbEventDeliveryRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken cancellationToken = default)
    {
        var outcomeStr = outcome.ToString();

        return await Entities
            .Where(r => r.Outcome == outcomeStr)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DbEventDeliveryRecord>> GetByTimeRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        return await Entities
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(cancellationToken);
    }
}
