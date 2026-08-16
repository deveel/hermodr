//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr;

/// <summary>
/// A concrete Entity Framework-friendly dead-letter entity that stores a <see cref="CloudEvent"/>.
/// </summary>
public class DbDeadLetterMessage : IDeadLetterMessage
{
    private static readonly HashSet<string> StandardAttributeNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "specversion", "id", "type", "source",
            "time", "subject", "datacontenttype", "dataschema"
        };

    /// <summary>
    /// Gets the unique identifier of the stored CloudEvent.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets the CloudEvents specification version used by the stored event.
    /// </summary>
    public string SpecVersion { get; set; } = CloudEventsSpecVersion.V1_0.VersionId;

    /// <summary>
    /// Gets the type of the stored CloudEvent.
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// Gets the source URI of the stored CloudEvent.
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>
    /// Gets the optional subject of the stored CloudEvent.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets the optional time at which the stored CloudEvent occurred.
    /// </summary>
    public DateTimeOffset? EventTime { get; set; }

    /// <summary>
    /// Gets the optional media type of the event payload.
    /// </summary>
    public string? DataContentType { get; set; }

    /// <summary>
    /// Gets the optional schema URI describing the event payload.
    /// </summary>
    public string? DataSchema { get; set; }

    /// <summary>
    /// Gets the event payload serialized as text, when available.
    /// </summary>
    public string? DataText { get; set; }

    /// <summary>
    /// Gets the event payload as a binary value, when available.
    /// </summary>
    public byte[]? DataBytes { get; set; }

    /// <summary>
    /// Gets the collection of persisted CloudEvent extension attributes associated with the message.
    /// </summary>
    public ICollection<DbDeadLetterAttribute> Attributes { get; set; } = new List<DbDeadLetterAttribute>();

    /// <summary>
    /// Gets the name of the publisher pipeline associated with the failed delivery.
    /// </summary>
    public string PublisherName { get; set; } = String.Empty;

    /// <summary>
    /// Gets the optional logical name of the channel that failed delivery.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// Gets the optional assembly-qualified type name of the channel that failed delivery.
    /// </summary>
    public string? ChannelType { get; set; }

    /// <summary>
    /// Gets the optional last error message recorded for the dead-letter message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the replay lifecycle status of the dead-letter message.
    /// </summary>
    public DeadLetterMessageStatus Status { get; set; } = DeadLetterMessageStatus.Pending;

    /// <summary>
    /// Gets the number of replay attempts made for the dead-letter message.
    /// </summary>
    public int ReplayCount { get; set; }

    /// <summary>
    /// Gets the optional UTC time at which the message becomes eligible for replay.
    /// </summary>
    public DateTimeOffset? NextReplayAt { get; set; }

    /// <summary>
    /// Gets the UTC time at which the dead-letter message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = EventSystemTime.Instance.UtcNow;

    /// <summary>
    /// Gets the optional UTC time at which the status was last updated.
    /// </summary>
    public DateTimeOffset? LastStatusAt { get; set; }

    CloudEvent IDeadLetterMessage.Event => BuildCloudEvent();

    /// <summary>
    /// Reconstructs the CloudEvent represented by the stored properties and attributes.
    /// </summary>
    /// <returns>A reconstructed <see cref="CloudEvent"/> instance populated from the stored attributes.</returns>
    protected virtual CloudEvent BuildCloudEvent()
    {
        var specVersion = SpecVersion == CloudEventsSpecVersion.V1_0.VersionId
            ? CloudEventsSpecVersion.V1_0
            : CloudEventsSpecVersion.Default;

        var cloudEvent = new CloudEvent(specVersion)
        {
            Id = Id,
            Type = EventType,
            Source = new Uri(Source, UriKind.RelativeOrAbsolute),
            Subject = Subject,
            Time = EventTime,
            DataContentType = DataContentType,
            DataSchema = DataSchema is not null ? new Uri(DataSchema, UriKind.RelativeOrAbsolute) : null
        };

        foreach (var attribute in Attributes)
            attribute.ApplyTo(cloudEvent);

        if (DataText is not null)
            cloudEvent.Data = DataText;
        else if (DataBytes is not null)
            cloudEvent.Data = DataBytes;

        return cloudEvent;
    }

    /// <summary>
    /// Populates the entity from the specified dead-letter context using the current system time.
    /// </summary>
    /// <param name="context">The dead-letter context describing the failed delivery.</param>
    public virtual void PopulateFromDeadLetterContext(DeadLetterContext context)
        => PopulateFromDeadLetterContext(context, EventSystemTime.Instance.UtcNow);

    /// <summary>
    /// Populates the entity from the specified dead-letter context, capturing the supplied creation time.
    /// </summary>
    /// <param name="context">The dead-letter context describing the failed delivery.</param>
    /// <param name="createdAt">The UTC time at which the dead-letter message is considered created.</param>
    public virtual void PopulateFromDeadLetterContext(DeadLetterContext context, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cloudEvent = context.Event;
        Id = cloudEvent.Id ?? Guid.NewGuid().ToString("N");
        SpecVersion = cloudEvent.SpecVersion?.VersionId ?? CloudEventsSpecVersion.V1_0.VersionId;
        EventType = cloudEvent.Type ?? throw new ArgumentException("CloudEvent.Type is required.", nameof(context));
        Source = cloudEvent.Source?.ToString() ?? throw new ArgumentException("CloudEvent.Source is required.", nameof(context));
        Subject = cloudEvent.Subject;
        EventTime = cloudEvent.Time;
        DataContentType = cloudEvent.DataContentType;
        DataSchema = cloudEvent.DataSchema?.ToString();
        PublisherName = context.PublisherName;
        ChannelName = context.ChannelName;
        ChannelType = context.ChannelType?.AssemblyQualifiedName ?? context.ChannelType?.FullName;
        ErrorMessage = context.Exception.Message;
        Status = DeadLetterMessageStatus.Pending;
        ReplayCount = 0;
        NextReplayAt = null;
        CreatedAt = createdAt;
        LastStatusAt = null;

        DataText = null;
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
                DataText = System.Text.Json.JsonSerializer.Serialize(cloudEvent.Data);
                break;
        }

        Attributes.Clear();
        foreach (var (attribute, value) in cloudEvent.GetPopulatedAttributes())
        {
            if (StandardAttributeNames.Contains(attribute.Name) || value is null)
                continue;

            Attributes.Add(DbDeadLetterAttribute.FromAttribute(Id, attribute, value));
        }
    }
}
