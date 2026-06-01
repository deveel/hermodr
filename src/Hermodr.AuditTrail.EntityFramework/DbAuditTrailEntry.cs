using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Text;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Hermodr;

/// <summary>
/// An Entity Framework Core entity that represents an audit trail entry,
/// implementing <see cref="IAuditTrailEntry"/>.
/// </summary>
public class DbAuditTrailEntry : IAuditTrailEntry
{
    private static readonly JsonEventFormatter EventFormatter = new();
    private static readonly ContentType CloudEventsContentType = new("application/cloudevents+json");

    /// <summary>
    /// Gets or sets the unique identifier of the audit trail entry.
    /// </summary>
    [Key]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the CloudEvent.
    /// </summary>
    public string EventId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the type of the CloudEvent.
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the C# class name of the event data, if known.
    /// </summary>
    public string? EventDataClassName { get; set; }

    /// <summary>
    /// Gets or sets the source of the CloudEvent.
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>
    /// Gets or sets the subject of the CloudEvent, if any.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the CloudEvent.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the content type of the event data, if any.
    /// </summary>
    public string? DataContentType { get; set; }

    /// <summary>
    /// Gets or sets the schema URI of the event data, if any.
    /// </summary>
    public string? DataSchema { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON representation of the CloudEvent.
    /// </summary>
    public string? EventData { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this entry was written to the audit trail.
    /// </summary>
    public DateTimeOffset StoredAt { get; set; }

    /// <summary>
    /// Gets or sets the collection of custom extension attributes for this entry.
    /// </summary>
    public virtual ICollection<DbAuditTrailAttribute> Attributes { get; set; } = new List<DbAuditTrailAttribute>();

    /// <summary>
    /// Gets the custom extension attributes of the CloudEvent.
    /// </summary>
    IDictionary<string, string>? IAuditTrailEntry.ExtensionAttributes
        => Attributes.ToDictionary(a => a.Key, a => a.Value);

    /// <summary>
    /// Converts this entity to an <see cref="AuditTrailEntry"/>.
    /// </summary>
    /// <returns>
    /// An <see cref="AuditTrailEntry"/> populated with the values from this entity.
    /// </returns>
    public AuditTrailEntry ToEntry()
    {
        return new AuditTrailEntry
        {
            Id = Id,
            EventId = EventId,
            EventType = EventType,
            EventDataClassName = EventDataClassName,
            Source = Source,
            Subject = Subject,
            Timestamp = Timestamp,
            DataContentType = DataContentType,
            DataSchema = DataSchema,
            EventData = EventData,
            ExtensionAttributes = Attributes?.ToDictionary(a => a.Key, a => a.Value),
            StoredAt = StoredAt
        };
    }

    /// <summary>
    /// Creates a new <see cref="DbAuditTrailEntry"/> from an <see cref="IAuditTrailEntry"/>.
    /// </summary>
    /// <param name="entry">
    /// The source entry to copy from.
    /// </param>
    /// <returns>
    /// A new <see cref="DbAuditTrailEntry"/> populated with the values from the source.
    /// </returns>
    public static DbAuditTrailEntry FromEntry(IAuditTrailEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var attributes = new List<DbAuditTrailAttribute>();
        if (entry.ExtensionAttributes != null)
        {
            foreach (var attr in entry.ExtensionAttributes)
            {
                attributes.Add(new DbAuditTrailAttribute
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Key = attr.Key,
                    Value = attr.Value
                });
            }
        }

        return new DbAuditTrailEntry
        {
            Id = entry.Id,
            EventId = entry.EventId,
            EventType = entry.EventType,
            EventDataClassName = entry.EventDataClassName,
            Source = entry.Source,
            Subject = entry.Subject,
            Timestamp = entry.Timestamp,
            DataContentType = entry.DataContentType,
            DataSchema = entry.DataSchema,
            EventData = entry.EventData,
            StoredAt = entry.StoredAt,
            Attributes = attributes
        };
    }
}
