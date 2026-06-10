using System.Runtime.CompilerServices;

using Kista;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hermodr;

/// <summary>
/// An Entity Framework-backed repository for audit trail entries.
/// </summary>
public class EntityAuditTrailRepository : EntityRepository<DbAuditTrailEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityAuditTrailRepository"/> class.
    /// </summary>
    /// <param name="context">The <see cref="AuditTrailDbContext"/> to use for data access.</param>
    /// <param name="services">An optional <see cref="IServiceProvider"/> for dependency resolution.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public EntityAuditTrailRepository(AuditTrailDbContext context, IServiceProvider? services = null, ILogger<EntityRepository<DbAuditTrailEntry>>? logger = null) 
        : base(context, services, logger)
    {
    }
    
    /// <summary>
    /// Streams audit trail entries matching the given query filters.
    /// </summary>
    /// <param name="query">The query defining the filters to apply.</param>
    /// <param name="cancellationToken">A token to cancel the streaming operation.</param>
    /// <returns>An async enumerable of matching <see cref="DbAuditTrailEntry"/> instances.</returns>
    public async IAsyncEnumerable<DbAuditTrailEntry> StreamEntiesAsync(AuditTrailStreamQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        
        IQueryable<DbAuditTrailEntry> queryable = Context.Set<DbAuditTrailEntry>()
            .Include(e => e.Attributes)
            .AsNoTracking();

        if (query.EventType != null)
            queryable = queryable.Where(e => e.EventType == query.EventType);

        if (query.EventDataClassName != null)
            queryable = queryable.Where(e => e.EventDataClassName == query.EventDataClassName);

        if (query.Source != null)
            queryable = queryable.Where(e => e.Source == query.Source);

        if (query.Subject != null)
            queryable = queryable.Where(e => e.Subject == query.Subject);

        if (query.From.HasValue)
        {
            var from = query.From.Value;
            queryable = queryable.Where(e => e.Timestamp >= from);
        }

        if (query.To.HasValue)
        {
            var to = query.To.Value;
            queryable = queryable.Where(e => e.Timestamp <= to);
        }

        if (query.Attributes != null)
        {
            foreach (var attr in query.Attributes)
            {
                var key = attr.Key;
                var value = attr.Value;
                queryable = queryable.Where(e => e.Attributes.Any(a => a.Key == key && a.Value == value));
            }
        }

        queryable = queryable.OrderBy(e => e.Timestamp);

        await foreach (var dbEntry in queryable.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return dbEntry;
        }
    }
}