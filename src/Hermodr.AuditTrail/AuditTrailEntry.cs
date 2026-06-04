using System.ComponentModel.DataAnnotations;

namespace Hermodr;

/// <summary>
/// The default implementation of <see cref="IAuditTrailEntry"/> that represents
/// a record in the audit trail.
/// </summary>
public class AuditTrailEntry : IAuditTrailEntry
{
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
    /// Gets or sets the custom extension attributes of the CloudEvent.
    /// </summary>
    public IDictionary<string, string>? ExtensionAttributes { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this entry was written to the audit trail.
    /// </summary>
    public DateTimeOffset StoredAt { get; set; }

    /// <summary>
    /// Creates a new <see cref="AuditTrailEntry"/> from a <see cref="CloudNative.CloudEvents.CloudEvent"/>.
    /// </summary>
    /// <param name="cloudEvent">
    /// The CloudEvent to create the entry from.
    /// </param>
    /// <param name="dataClassName">
    /// An optional C# class name of the event data.
    /// </param>
    /// <param name="storedAt">
    /// The UTC timestamp when this entry is being stored. If <c>null</c>, <see cref="DateTimeOffset.UtcNow"/> is used.
    /// </param>
    /// <returns>
    /// A new <see cref="AuditTrailEntry"/> populated from the CloudEvent.
    /// </returns>
    public static AuditTrailEntry FromCloudEvent(
        CloudNative.CloudEvents.CloudEvent cloudEvent,
        string? dataClassName = null,
        DateTimeOffset? storedAt = null)
    {
        ArgumentNullException.ThrowIfNull(cloudEvent);

        string? eventData = null;
        try
        {
            var formatter = new CloudNative.CloudEvents.SystemTextJson.JsonEventFormatter();
            var memory = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            eventData = System.Text.Encoding.UTF8.GetString(memory.ToArray());
        }
        catch (ArgumentException)
        {
            // ignore serialization errors
        }
        catch (InvalidOperationException)
        {
            // ignore serialization errors
        }

        var extensionAttributes = new Dictionary<string, string>();
        foreach (var attr in cloudEvent.GetPopulatedAttributes())
        {
            if (!StandardCloudEventAttributes.IsStandard(attr.Key.Name))
            {
                extensionAttributes[attr.Key.ToString()] = attr.Value?.ToString() ?? string.Empty;
            }
        }

        return new AuditTrailEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = cloudEvent.Id ?? string.Empty,
            EventType = cloudEvent.Type ?? string.Empty,
            EventDataClassName = dataClassName,
            Source = cloudEvent.Source?.ToString() ?? string.Empty,
            Subject = cloudEvent.Subject,
            Timestamp = cloudEvent.Time ?? DateTimeOffset.UtcNow,
            DataContentType = cloudEvent.DataContentType,
            DataSchema = cloudEvent.DataSchema?.ToString(),
            EventData = eventData,
            ExtensionAttributes = extensionAttributes.Count > 0 ? extensionAttributes : null,
            StoredAt = storedAt ?? DateTimeOffset.UtcNow
        };
    }
}
