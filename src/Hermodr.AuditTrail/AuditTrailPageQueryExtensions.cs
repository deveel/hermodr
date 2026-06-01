using Kista;

namespace Hermodr;

/// <summary>
/// Provides extension methods on <see cref="PageQuery{T}"/> for filtering
/// audit trail entries.
/// </summary>
public static class AuditTrailPageQueryExtensions
{
    /// <summary>
    /// Filters the query to include only entries with the specified CloudEvent type.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the audit trail entry.
    /// </typeparam>
    /// <param name="query">
    /// The page query to extend.
    /// </param>
    /// <param name="eventType">
    /// The CloudEvent type to filter by.
    /// </param>
    /// <returns>
    /// The page query with the filter applied.
    /// </returns>
    public static PageQuery<T> OfType<T>(this PageQuery<T> query, string eventType)
        where T : class, IAuditTrailEntry
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return query.Where(e => e.EventType == eventType);
    }

    /// <summary>
    /// Filters the query to include only entries with the specified data class name.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the audit trail entry.
    /// </typeparam>
    /// <param name="query">
    /// The page query to extend.
    /// </param>
    /// <param name="className">
    /// The C# class name to filter by.
    /// </param>
    /// <returns>
    /// The page query with the filter applied.
    /// </returns>
    public static PageQuery<T> OfDataType<T>(this PageQuery<T> query, string className)
        where T : class, IAuditTrailEntry
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        return query.Where(e => e.EventDataClassName == className);
    }

    /// <summary>
    /// Filters the query to include only entries with the specified data type.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the audit trail entry.
    /// </typeparam>
    /// <param name="query">
    /// The page query to extend.
    /// </param>
    /// <param name="dataType">
    /// The C# type to filter by.
    /// </param>
    /// <returns>
    /// The page query with the filter applied.
    /// </returns>
    public static PageQuery<T> OfDataType<T>(this PageQuery<T> query, Type dataType)
        where T : class, IAuditTrailEntry
    {
        ArgumentNullException.ThrowIfNull(dataType);
        return query.Where(e => e.EventDataClassName == dataType.FullName);
    }

    /// <summary>
    /// Filters the query to include only entries with the specified CloudEvent source.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the audit trail entry.
    /// </typeparam>
    /// <param name="query">
    /// The page query to extend.
    /// </param>
    /// <param name="source">
    /// The CloudEvent source to filter by.
    /// </param>
    /// <returns>
    /// The page query with the filter applied.
    /// </returns>
    public static PageQuery<T> FromSource<T>(this PageQuery<T> query, string source)
        where T : class, IAuditTrailEntry
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return query.Where(e => e.Source == source);
    }

    /// <summary>
    /// Filters the query to include only entries with the specified CloudEvent subject.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the audit trail entry.
    /// </typeparam>
    /// <param name="query">
    /// The page query to extend.
    /// </param>
    /// <param name="subject">
    /// The CloudEvent subject to filter by.
    /// </param>
    /// <returns>
    /// The page query with the filter applied.
    /// </returns>
    public static PageQuery<T> WithSubject<T>(this PageQuery<T> query, string subject)
        where T : class, IAuditTrailEntry
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return query.Where(e => e.Subject == subject);
    }

    /// <summary>
    /// Filters the query to include only entries within the specified time range.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the audit trail entry.
    /// </typeparam>
    /// <param name="query">
    /// The page query to extend.
    /// </param>
    /// <param name="from">
    /// The start of the time range.
    /// </param>
    /// <param name="to">
    /// The end of the time range.
    /// </param>
    /// <returns>
    /// The page query with the filter applied.
    /// </returns>
    public static PageQuery<T> InTimeRange<T>(this PageQuery<T> query, DateTimeOffset from, DateTimeOffset to)
        where T : class, IAuditTrailEntry
    {
        return query.Where(e => e.Timestamp >= from && e.Timestamp <= to);
    }

    /// <summary>
    /// Filters the query to include only entries with the specified custom attribute.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the audit trail entry.
    /// </typeparam>
    /// <param name="query">
    /// The page query to extend.
    /// </param>
    /// <param name="key">
    /// The attribute key to filter by.
    /// </param>
    /// <param name="value">
    /// The attribute value to filter by.
    /// </param>
    /// <returns>
    /// The page query with the filter applied.
    /// </returns>
    public static PageQuery<T> WithAttribute<T>(this PageQuery<T> query, string key, string value)
        where T : class, IAuditTrailEntry
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return query.Where(e => e.ExtensionAttributes != null && e.ExtensionAttributes.ContainsKey(key) && e.ExtensionAttributes[key] == value);
    }

    /// <summary>
    /// Orders the query results by timestamp.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the audit trail entry.
    /// </typeparam>
    /// <param name="query">
    /// The page query to extend.
    /// </param>
    /// <param name="descending">
    /// If <c>true</c>, orders descending; otherwise ascending.
    /// </param>
    /// <returns>
    /// The page query with the ordering applied.
    /// </returns>
    public static PageQuery<T> OrderedByTimestamp<T>(this PageQuery<T> query, bool descending = false)
        where T : class, IAuditTrailEntry
    {
        return descending ? query.OrderByDescending(e => e.Timestamp) : query.OrderBy(e => e.Timestamp);
    }
}
