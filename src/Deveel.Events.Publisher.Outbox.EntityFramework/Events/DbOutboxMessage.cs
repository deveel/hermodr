//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// A concrete, Entity Framework–friendly outbox message entity that stores a
/// <see cref="CloudEvent"/> in a relational database using a normalised schema.
/// </summary>
/// <remarks>
/// <para>
/// Well-known CloudEvents context attributes (<c>id</c>, <c>type</c>,
/// <c>source</c>, <c>specversion</c>, <c>subject</c>, <c>time</c>,
/// <c>datacontenttype</c>, <c>dataschema</c>) are mapped as scalar columns on
/// this table.  The event payload is stored in <see cref="DataText"/> (for
/// string / JSON data) or <see cref="DataBytes"/> (for raw binary data).
/// Extension attributes are stored as child rows in <see cref="Attributes"/>
/// via a one-to-many relationship with <see cref="DbCloudEventAttribute"/>.
/// </para>
/// <para>
/// Subclass this entity and override <see cref="BuildCloudEvent"/> if you need
/// custom reconstruction logic (e.g. decrypting the payload before passing it
/// to the relay).
/// </para>
/// <para>
/// Use <see cref="PopulateFromCloudEvent"/> inside your
/// <see cref="IOutboxMessageFactory{TMessage}"/> implementation to populate
/// all mapped columns from a live <see cref="CloudEvent"/> instance.
/// </para>
/// </remarks>
public class DbOutboxMessage : IOutboxMessage
{
    private static readonly HashSet<string> StandardAttributeNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "specversion", "id", "type", "source",
            "time", "subject", "datacontenttype", "dataschema"
        };

    // ── Entity identity ───────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the primary key of the outbox row.
    /// Initialised from <see cref="CloudEvent.Id"/> by
    /// <see cref="PopulateFromCloudEvent"/>; a new GUID string is generated
    /// when the CloudEvent carries no <c>id</c>.
    /// </summary>
    public string Id { get; set; } = null!;

    // ── CloudEvent required context attributes ────────────────────────────────

    /// <summary>
    /// Gets or sets the CloudEvents specification version.
    /// Always <c>"1.0"</c> for events produced by this framework.
    /// </summary>
    public string SpecVersion { get; set; } = CloudEventsSpecVersion.V1_0.VersionId;

    /// <summary>
    /// Gets or sets the CloudEvents <c>type</c> attribute (e.g.
    /// <c>"com.example.order.placed"</c>).
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CloudEvents <c>source</c> attribute stored as its
    /// absolute or relative URI string.
    /// </summary>
    public string Source { get; set; } = null!;

    // ── CloudEvent optional context attributes ────────────────────────────────

    /// <summary>
    /// Gets or sets the CloudEvents <c>subject</c> attribute, or <c>null</c>
    /// when not present.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the CloudEvents <c>time</c> attribute, or <c>null</c>
    /// when not present.
    /// </summary>
    public DateTimeOffset? EventTime { get; set; }

    /// <summary>
    /// Gets or sets the CloudEvents <c>datacontenttype</c> attribute (e.g.
    /// <c>"application/json"</c>), or <c>null</c> when not present.
    /// </summary>
    public string? DataContentType { get; set; }

    /// <summary>
    /// Gets or sets the CloudEvents <c>dataschema</c> attribute stored as its
    /// URI string, or <c>null</c> when not present.
    /// </summary>
    public string? DataSchema { get; set; }

    // ── CloudEvent data payload ───────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the event data as a UTF-8 text or JSON string.
    /// Mutually exclusive with <see cref="DataBytes"/>: only one of the two
    /// should be set for a given row.
    /// </summary>
    public string? DataText { get; set; }

    /// <summary>
    /// Gets or sets the event data as a raw binary blob.
    /// Mutually exclusive with <see cref="DataText"/>: only one of the two
    /// should be set for a given row.
    /// </summary>
    public byte[]? DataBytes { get; set; }

    // ── Extension attributes (one-to-many) ────────────────────────────────────

    /// <summary>
    /// Gets or sets the collection of CloudEvents extension attributes stored
    /// as child rows in the <c>CloudEventAttributes</c> table.
    /// </summary>
    public ICollection<DbCloudEventAttribute> Attributes { get; set; } =
        new List<DbCloudEventAttribute>();

    // ── Outbox delivery tracking ──────────────────────────────────────────────

    /// <inheritdoc cref="IOutboxMessage.Status"/>
    public OutboxMessageStatus Status { get; set; }

    /// <inheritdoc cref="IOutboxMessage.ErrorMessage"/>
    public string? ErrorMessage { get; set; }

    /// <inheritdoc cref="IOutboxMessage.RetryCount"/>
    public int RetryCount { get; set; }

    /// <inheritdoc cref="IOutboxMessage.NextRetryAt"/>
    public DateTimeOffset? NextRetryAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp at which this message was first created
    /// and inserted into the outbox table.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent status transition,
    /// or <c>null</c> if the status has not yet changed from the initial
    /// <see cref="OutboxMessageStatus.Pending"/>.
    /// </summary>
    public DateTimeOffset? LastStatusAt { get; set; }

    // ── IOutboxMessage ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    CloudEvent IOutboxMessage.Event => BuildCloudEvent();

    // ── CloudEvent reconstruction ─────────────────────────────────────────────

    /// <summary>
    /// Reconstructs the <see cref="CloudEvent"/> from the columns persisted on
    /// this entity and its <see cref="Attributes"/> child rows.
    /// </summary>
    /// <returns>
    /// A fully populated <see cref="CloudEvent"/> ready for publishing.
    /// </returns>
    /// <remarks>
    /// Override this method in a subclass when you need to transform the
    /// stored data before returning the event (e.g. to decrypt the payload).
    /// </remarks>
    protected virtual CloudEvent BuildCloudEvent()
    {
        var specVersion = SpecVersion == CloudEventsSpecVersion.V1_0.VersionId
            ? CloudEventsSpecVersion.V1_0
            : CloudEventsSpecVersion.Default;

        var cloudEvent = new CloudEvent(specVersion)
        {
            Id              = Id,
            Type            = EventType,
            Source          = new Uri(Source, UriKind.RelativeOrAbsolute),
            Subject         = Subject,
            Time            = EventTime,
            DataContentType = DataContentType,
            DataSchema      = DataSchema is not null
                                  ? new Uri(DataSchema, UriKind.RelativeOrAbsolute)
                                  : null
        };

        // Restore extension attributes
        foreach (var attr in Attributes)
            attr.ApplyTo(cloudEvent);

        // Restore the data payload (text takes precedence when both are set)
        if (DataText is not null)
            cloudEvent.Data = DataText;
        else if (DataBytes is not null)
            cloudEvent.Data = DataBytes;

        return cloudEvent;
    }

    // ── Factory helper ────────────────────────────────────────────────────────

    /// <summary>
    /// Populates all CloudEvent-related columns and the <see cref="Attributes"/>
    /// collection from a live <see cref="CloudEvent"/> instance.
    /// </summary>
    /// <param name="cloudEvent">
    /// The source event.  Must carry at least the four required CloudEvents
    /// attributes (<c>id</c>, <c>type</c>, <c>source</c>, <c>specversion</c>).
    /// </param>
    /// <remarks>
    /// Call this method inside your <see cref="IOutboxMessageFactory{TMessage}"/>
    /// implementation after constructing the <see cref="DbOutboxMessage"/>
    /// instance:
    /// <code language="csharp">
    /// public MyOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
    /// {
    ///     var message = new MyOutboxMessage();
    ///     message.PopulateFromCloudEvent(cloudEvent);
    ///     return message;
    /// }
    /// </code>
    /// </remarks>
    public virtual void PopulateFromCloudEvent(CloudEvent cloudEvent)
    {
        Id          = cloudEvent.Id ?? Guid.NewGuid().ToString("N");
        SpecVersion = cloudEvent.SpecVersion?.VersionId ?? CloudEventsSpecVersion.V1_0.VersionId;
        EventType   = cloudEvent.Type
                      ?? throw new ArgumentException(
                             "CloudEvent.Type is required.", nameof(cloudEvent));
        Source      = cloudEvent.Source?.ToString()
                      ?? throw new ArgumentException(
                             "CloudEvent.Source is required.", nameof(cloudEvent));
        Subject         = cloudEvent.Subject;
        EventTime       = cloudEvent.Time;
        DataContentType = cloudEvent.DataContentType;
        DataSchema      = cloudEvent.DataSchema?.ToString();

        // Data payload
        DataText  = null;
        DataBytes = null;
        switch (cloudEvent.Data)
        {
            case byte[] bytes:
                DataBytes = bytes;
                break;
            case string text:
                DataText = text;
                break;
            case null:
                break;
            default:
                // Fallback: serialise unknown types as JSON text
                DataText = System.Text.Json.JsonSerializer.Serialize(cloudEvent.Data);
                break;
        }

        // Extension attributes
        Attributes.Clear();
        foreach (var (attr, value) in cloudEvent.GetPopulatedAttributes())
        {
            if (StandardAttributeNames.Contains(attr.Name)) continue;
            if (value is null) continue;

            Attributes.Add(DbCloudEventAttribute.FromAttribute(Id, attr, value));
        }
    }
}