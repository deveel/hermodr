using Deveel.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Deveel.Events;

/// <summary>
/// An implementation of <see cref="IEventDeliveryLogRepository"/> that stores delivery
/// records in a relational database using Entity Framework Core.
/// </summary>
public class EntityEventDeliveryLogRepository : EntityRepository<DbEventDeliveryRecord, string>, IEventDeliveryLogRepository
{
    private readonly IEventSystemTime _systemTime;

    /// <summary>
    /// Creates a new instance of <see cref="EntityEventDeliveryLogRepository"/>.
    /// </summary>
    /// <param name="context">
    /// The database context to use for data access.
    /// </param>
    /// <param name="systemTime">
    /// An optional service for obtaining the current UTC time; defaults to <see cref="EventSystemTime.Instance"/>.
    /// </param>
    /// <param name="logger">
    /// An optional logger for diagnostic output.
    /// </param>
    public EntityEventDeliveryLogRepository(
        DeliveryLogDbContext context,
        IEventSystemTime? systemTime = null,
        ILogger<EntityEventDeliveryLogRepository>? logger = null)
        : base(context, logger)
    {
        _systemTime = systemTime ?? EventSystemTime.Instance;
    }

    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    public string ProviderName => "EntityFrameworkCore";

    /// <summary>
    /// Records a delivery attempt in the database.
    /// </summary>
    /// <param name="record">
    /// The record of the event delivery to be stored.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    public async Task RecordAsync(IEventDeliveryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entity = record as DbEventDeliveryRecord ?? DbEventDeliveryRecord.FromRecord(record);

        await AddAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Retrieves all delivery records associated with the given event identifier.
    /// </summary>
    /// <param name="eventId">The identifier of the event.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records for the specified event.
    /// </returns>
    public async Task<IReadOnlyList<EventDeliveryRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        var entities = await Entities
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToRecord()).ToList().AsReadOnly();
    }

    /// <summary>
    /// Retrieves all delivery records for the given channel name.
    /// </summary>
    /// <param name="channelName">The name of the channel.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records for the specified channel.
    /// </returns>
    public async Task<IReadOnlyList<EventDeliveryRecord>> GetByChannelAsync(string channelName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        var entities = await Entities
            .Where(r => r.ChannelName != null && r.ChannelName == channelName)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToRecord()).ToList().AsReadOnly();
    }

    /// <summary>
    /// Retrieves all delivery records matching the specified outcome.
    /// </summary>
    /// <param name="outcome">The delivery outcome to filter by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of delivery records with the specified outcome.
    /// </returns>
    public async Task<IReadOnlyList<EventDeliveryRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken cancellationToken = default)
    {
        var outcomeStr = outcome.ToString();

        var entities = await Entities
            .Where(r => r.Outcome == outcomeStr)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToRecord()).ToList().AsReadOnly();
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
    public async Task<IReadOnlyList<EventDeliveryRecord>> GetByTimeRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var entities = await Entities
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToRecord()).ToList().AsReadOnly();
    }

    #region IRepository<EventDeliveryRecord, object> explicit implementation

    async Task IRepository<EventDeliveryRecord, object>.AddAsync(EventDeliveryRecord entity, CancellationToken cancellationToken)
    {
        var dbEntity = DbEventDeliveryRecord.FromRecord(entity);
        await AddAsync(dbEntity, cancellationToken);
    }

    async Task<bool> IRepository<EventDeliveryRecord, object>.UpdateAsync(EventDeliveryRecord entity, CancellationToken cancellationToken)
    {
        var dbEntity = DbEventDeliveryRecord.FromRecord(entity);
        return await UpdateAsync(dbEntity, cancellationToken);
    }

    async Task<bool> IRepository<EventDeliveryRecord, object>.RemoveAsync(EventDeliveryRecord entity, CancellationToken cancellationToken)
    {
        var dbEntity = DbEventDeliveryRecord.FromRecord(entity);
        return await RemoveAsync(dbEntity, cancellationToken);
    }

    async Task IRepository<EventDeliveryRecord, object>.AddRangeAsync(IEnumerable<EventDeliveryRecord> entities, CancellationToken cancellationToken)
    {
        var dbEntities = entities.Select(DbEventDeliveryRecord.FromRecord).ToList();
        await AddRangeAsync(dbEntities, cancellationToken);
    }

    async Task IRepository<EventDeliveryRecord, object>.RemoveRangeAsync(IEnumerable<EventDeliveryRecord> entities, CancellationToken cancellationToken)
    {
        var dbEntities = entities.Select(DbEventDeliveryRecord.FromRecord).ToList();
        await RemoveRangeAsync(dbEntities, cancellationToken);
    }

    async Task<EventDeliveryRecord?> IRepository<EventDeliveryRecord, object>.FindAsync(object key, CancellationToken cancellationToken)
    {
        if (key is not string s)
            throw new ArgumentException($"The key must be a string, but got '{key?.GetType()}'", nameof(key));

        var dbEntity = await FindAsync(s, cancellationToken);
        return dbEntity?.ToRecord();
    }

    object? IRepository<EventDeliveryRecord, object>.GetEntityKey(EventDeliveryRecord entity) => entity.Id;

    #endregion
}
