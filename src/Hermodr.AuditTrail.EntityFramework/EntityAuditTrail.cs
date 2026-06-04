using System.Runtime.CompilerServices;

using CloudNative.CloudEvents;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hermodr;

/// <summary>
/// An Entity Framework Core implementation of <see cref="IAuditTrailWriter"/> and
/// <see cref="IAuditTrailReader{TEntry}"/> that stores audit trail entries in a
/// relational database.
/// </summary>
public class EntityAuditTrail : IAuditTrailWriter, IAuditTrailReader<DbAuditTrailEntry>
{
    private readonly AuditTrailDbContext _context;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a new instance of <see cref="EntityAuditTrail"/>.
    /// </summary>
    /// <param name="context">
    /// The database context to use for data access.
    /// </param>
    /// <param name="logger">
    /// An optional logger.
    /// </param>
    public EntityAuditTrail(
        AuditTrailDbContext context,
        ILogger<EntityAuditTrail>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? NullLogger<EntityAuditTrail>.Instance;
    }

    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    public string ProviderName => "EntityFrameworkCore";

    /// <summary>
    /// Appends a CloudEvent to the audit trail database.
    /// </summary>
    /// <param name="event">
    /// The CloudEvent to append.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    public async Task AppendAsync(CloudEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var entry = AuditTrailEntry.FromCloudEvent(@event);
        var dbEntry = DbAuditTrailEntry.FromEntry(entry);

        _context.AuditTrailEntries.Add(dbEntry);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogAuditTrailEntrySavedToDatabase(dbEntry.EventId, dbEntry.EventType);
    }

    /// <summary>
    /// Reads events from the audit trail database, optionally filtered by the query.
    /// </summary>
    /// <param name="query">
    /// An optional query to filter the results.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    /// <returns>
    /// An asynchronous enumerable of audit trail entries matching the query.
    /// </returns>
    public async IAsyncEnumerable<DbAuditTrailEntry> ReadAsync(
        AuditTrailStreamQuery? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IQueryable<DbAuditTrailEntry> queryable = _context.AuditTrailEntries
            .Include(e => e.Attributes)
            .AsNoTracking();

        if (query != null)
        {
            if (query.EventType != null)
                queryable = queryable.Where(e => e.EventType == query.EventType);

            if (query.EventDataClassName != null)
                queryable = queryable.Where(e => e.EventDataClassName == query.EventDataClassName);

            if (query.Source != null)
                queryable = queryable.Where(e => e.Source == query.Source);

            if (query.Subject != null)
                queryable = queryable.Where(e => e.Subject == query.Subject);

            if (query.From.HasValue)
                queryable = queryable.Where(e => e.Timestamp >= query.From.Value);

            if (query.To.HasValue)
                queryable = queryable.Where(e => e.Timestamp <= query.To.Value);

            if (query.Attributes != null)
            {
                foreach (var attr in query.Attributes)
                {
                    var key = attr.Key;
                    var value = attr.Value;
                    queryable = queryable.Where(e => e.Attributes.Any(a => a.Key == key && a.Value == value));
                }
            }
        }

        queryable = queryable.OrderBy(e => e.Timestamp);

        await foreach (var dbEntry in queryable.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return dbEntry;
        }
    }
}
