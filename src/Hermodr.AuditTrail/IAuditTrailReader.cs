namespace Hermodr;

/// <summary>
/// Provides a service to read events from the audit trail.
/// </summary>
/// <typeparam name="TEntry">
/// The type of the audit trail entry.
/// </typeparam>
public interface IAuditTrailReader<TEntry> where TEntry : class, IAuditTrailEntry
{
    /// <summary>
    /// Reads events from the audit trail, optionally filtered by the query.
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
    IAsyncEnumerable<TEntry> ReadAsync(
        AuditTrailStreamQuery? query = null,
        CancellationToken cancellationToken = default);
}
