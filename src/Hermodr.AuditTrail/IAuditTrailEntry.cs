namespace Hermodr;

/// <summary>
/// Represents a record in the audit trail, containing the event payload,
/// metadata, and CloudEvents attributes.
/// </summary>
public interface IAuditTrailEntry
{
    /// <summary>
    /// Gets the unique identifier of the audit trail entry.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the identifier of the CloudEvent.
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// Gets the type of the CloudEvent.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Gets the C# class name of the event data, if known.
    /// </summary>
    string? EventDataClassName { get; }

    /// <summary>
    /// Gets the source of the CloudEvent.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Gets the subject of the CloudEvent, if any.
    /// </summary>
    string? Subject { get; }

    /// <summary>
    /// Gets the timestamp of the CloudEvent.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the content type of the event data, if any.
    /// </summary>
    string? DataContentType { get; }

    /// <summary>
    /// Gets the schema URI of the event data, if any.
    /// </summary>
    string? DataSchema { get; }

    /// <summary>
    /// Gets the serialized JSON representation of the CloudEvent.
    /// </summary>
    string? EventData { get; }

    /// <summary>
    /// Gets the custom extension attributes of the CloudEvent.
    /// </summary>
    IDictionary<string, string>? ExtensionAttributes { get; }

    /// <summary>
    /// Gets the UTC timestamp when this entry was written to the audit trail.
    /// </summary>
    DateTimeOffset StoredAt { get; }
}
