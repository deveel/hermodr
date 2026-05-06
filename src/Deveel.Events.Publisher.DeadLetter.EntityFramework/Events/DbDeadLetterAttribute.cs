//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Represents a persisted CloudEvent extension attribute for a dead-letter message.
/// </summary>
public class DbDeadLetterAttribute
{
    public int Id { get; set; }

    public string MessageId { get; set; } = null!;

    public DbDeadLetterMessage Message { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Value { get; set; }

    public string ValueType { get; set; } = "string";

    internal CloudEventAttributeType GetAttributeType() => ValueType switch
    {
        "integer" => CloudEventAttributeType.Integer,
        "boolean" => CloudEventAttributeType.Boolean,
        "uri" => CloudEventAttributeType.Uri,
        "urireference" => CloudEventAttributeType.UriReference,
        "timestamp" => CloudEventAttributeType.Timestamp,
        "binary" => CloudEventAttributeType.Binary,
        _ => CloudEventAttributeType.String
    };

    internal void ApplyTo(CloudEvent cloudEvent)
    {
        if (Value is null)
            return;

        var type = GetAttributeType();
        var attribute = CloudEventAttribute.CreateExtension(Name, type);
        cloudEvent[attribute] = type.Parse(Value);
    }

    internal static DbDeadLetterAttribute FromAttribute(
        string messageId,
        CloudEventAttribute attribute,
        object value)
    {
        return new DbDeadLetterAttribute
        {
            MessageId = messageId,
            Name = attribute.Name,
            Value = attribute.Type.Format(value),
            ValueType = GetTypeName(attribute.Type)
        };
    }

    private static string GetTypeName(CloudEventAttributeType type)
    {
        if (type == CloudEventAttributeType.Integer) return "integer";
        if (type == CloudEventAttributeType.Boolean) return "boolean";
        if (type == CloudEventAttributeType.Uri) return "uri";
        if (type == CloudEventAttributeType.UriReference) return "urireference";
        if (type == CloudEventAttributeType.Timestamp) return "timestamp";
        if (type == CloudEventAttributeType.Binary) return "binary";
        return "string";
    }
}
