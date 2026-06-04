using System.Runtime.CompilerServices;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hermodr;

/// <summary>
/// An in-memory implementation of <see cref="IAuditTrailWriter"/> and
/// <see cref="IAuditTrailReader{TEntry}"/> that stores audit trail entries
/// in a volatile shared bag.
/// </summary>
public class InMemoryAuditTrail : IAuditTrailWriter, IAuditTrailReader<AuditTrailEntry>
{
    private readonly InMemorySharedBag<AuditTrailEntry> _bag;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a new instance of <see cref="InMemoryAuditTrail"/>.
    /// </summary>
    /// <param name="bag">
    /// The shared bag used to store entries.
    /// </param>
    /// <param name="logger">
    /// An optional logger.
    /// </param>
    public InMemoryAuditTrail(
        InMemorySharedBag<AuditTrailEntry> bag,
        ILogger<InMemoryAuditTrail>? logger = null)
    {
        _bag = bag ?? throw new ArgumentNullException(nameof(bag));
        _logger = logger ?? NullLogger<InMemoryAuditTrail>.Instance;
    }

    /// <summary>
    /// Appends a CloudEvent to the in-memory audit trail.
    /// </summary>
    /// <param name="event">
    /// The CloudEvent to append.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    public Task AppendAsync(CloudEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var entry = AuditTrailEntry.FromCloudEvent(@event);
        _bag.Add(entry);

        _logger.LogAuditTrailEntryAppended(entry.EventId, entry.EventType);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads events from the in-memory audit trail, optionally filtered by the query.
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
    public async IAsyncEnumerable<AuditTrailEntry> ReadAsync(
        AuditTrailStreamQuery? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entries = _bag.GetAll().AsEnumerable();

        if (query != null)
        {
            if (query.EventType != null)
                entries = entries.Where(e => e.EventType == query.EventType);

            if (query.EventDataClassName != null)
                entries = entries.Where(e => e.EventDataClassName == query.EventDataClassName);

            if (query.Source != null)
                entries = entries.Where(e => e.Source == query.Source);

            if (query.Subject != null)
                entries = entries.Where(e => e.Subject == query.Subject);

            var from = query.From;
            if (from is not null)
                entries = entries.Where(e => e.Timestamp >= from.Value);

            var to = query.To;
            if (to is not null)
                entries = entries.Where(e => e.Timestamp <= to.Value);

            if (query.Attributes != null)
            {
                foreach (var attr in query.Attributes)
                {
                    var key = attr.Key;
                    var value = attr.Value;
                    entries = entries.Where(e => e.ExtensionAttributes != null && e.ExtensionAttributes.ContainsKey(key) && e.ExtensionAttributes[key] == value);
                }
            }
        }

        foreach (var entry in entries.OrderBy(e => e.Timestamp))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
        }
    }
}
