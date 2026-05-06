//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

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

    public string Id { get; set; } = null!;

    public string SpecVersion { get; set; } = CloudEventsSpecVersion.V1_0.VersionId;

    public string EventType { get; set; } = null!;

    public string Source { get; set; } = null!;

    public string? Subject { get; set; }

    public DateTimeOffset? EventTime { get; set; }

    public string? DataContentType { get; set; }

    public string? DataSchema { get; set; }

    public string? DataText { get; set; }

    public byte[]? DataBytes { get; set; }

    public ICollection<DbDeadLetterAttribute> Attributes { get; set; } = new List<DbDeadLetterAttribute>();

    public string PublisherName { get; set; } = String.Empty;

    public string? ChannelName { get; set; }

    public string? ChannelType { get; set; }

    public string? ErrorMessage { get; set; }

    public DeadLetterMessageStatus Status { get; set; } = DeadLetterMessageStatus.Pending;

    public int ReplayCount { get; set; }

    public DateTimeOffset? NextReplayAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastStatusAt { get; set; }

    CloudEvent IDeadLetterMessage.Event => BuildCloudEvent();

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

    public virtual void PopulateFromDeadLetterContext(DeadLetterContext context)
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
        CreatedAt = DateTimeOffset.UtcNow;
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
