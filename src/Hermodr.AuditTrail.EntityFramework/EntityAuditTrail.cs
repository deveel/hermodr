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
    private readonly EntityAuditTrailRepository _context;
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
        EntityAuditTrailRepository repository,
        ILogger<EntityAuditTrail>? logger = null)
    {
        _context = repository ?? throw new ArgumentNullException(nameof(repository));
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
        
        await _context.AddAsync(dbEntry, cancellationToken);
        
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
    public IAsyncEnumerable<DbAuditTrailEntry> ReadAsync(
        AuditTrailStreamQuery? query = null, CancellationToken cancellationToken = default)
    {
        return _context.StreamEntiesAsync(query ?? new AuditTrailStreamQuery(), cancellationToken);
    }
}
