using System.Collections.Concurrent;

namespace Hermodr;

/// <summary>
/// A strongly-typed shared data store used by the in-memory audit trail
/// implementation to share state between the writer and reader.
/// </summary>
/// <typeparam name="TEntry">
/// The type of the audit trail entry.
/// </typeparam>
public sealed class InMemorySharedBag<TEntry> where TEntry : class, IAuditTrailEntry
{
    private readonly ConcurrentDictionary<string, TEntry> _entries = new();

    /// <summary>
    /// Adds an entry to the bag.
    /// </summary>
    /// <param name="entry">
    /// The entry to add.
    /// </param>
    public void Add(TEntry entry)
    {
        _entries[entry.Id] = entry;
    }

    /// <summary>
    /// Gets all entries in the bag.
    /// </summary>
    /// <returns>
    /// A read-only list of all entries.
    /// </returns>
    public IReadOnlyList<TEntry> GetAll()
    {
        return _entries.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Queries entries using a predicate.
    /// </summary>
    /// <param name="predicate">
    /// The predicate to filter entries.
    /// </param>
    /// <returns>
    /// An enumerable of matching entries.
    /// </returns>
    public IEnumerable<TEntry> Query(Func<TEntry, bool> predicate)
    {
        return _entries.Values.Where(predicate);
    }
}
