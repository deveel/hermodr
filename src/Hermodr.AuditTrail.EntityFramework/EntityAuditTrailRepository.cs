using System.Runtime.CompilerServices;

using Kista;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hermodr;

public class EntityAuditTrailRepository : EntityRepository<DbAuditTrailEntry>
{
    public EntityAuditTrailRepository(AuditTrailDbContext context, IServiceProvider? services = null, ILogger<EntityRepository<DbAuditTrailEntry>>? logger = null) 
        : base(context, services, logger)
    {
    }
    
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

        queryable = queryable.OrderBy(e => e.Timestamp);

        await foreach (var dbEntry in queryable.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return dbEntry;
        }
    }
}