namespace Hermodr;

/// <summary>
/// Represents a query for filtering events in the audit trail stream.
/// </summary>
public class AuditTrailStreamQuery
{
    /// <summary>
    /// Gets or sets the CloudEvent type to filter by.
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Gets or sets the C# class name of the event data to filter by.
    /// </summary>
    public string? EventDataClassName { get; set; }

    /// <summary>
    /// Gets or sets the CloudEvent source to filter by.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the CloudEvent subject to filter by.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the start of the time range to filter by.
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// Gets or sets the end of the time range to filter by.
    /// </summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Gets or sets the custom extension attributes to filter by.
    /// </summary>
    public IDictionary<string, string>? Attributes { get; set; }
}
