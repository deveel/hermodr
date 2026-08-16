//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr;

/// <summary>
/// Represents a persisted CloudEvent extension attribute for a dead-letter message.
/// </summary>
public class DbDeadLetterAttribute
{
    /// <summary>
    /// Gets the surrogate primary key of the attribute record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets the identifier of the dead-letter message this attribute belongs to.
    /// </summary>
    public string MessageId { get; set; } = null!;

    /// <summary>
    /// Gets the dead-letter message that owns this attribute.
    /// </summary>
    public DbDeadLetterMessage Message { get; set; } = null!;

    /// <summary>
    /// Gets the name of the CloudEvent extension attribute.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets the serialized value of the CloudEvent extension attribute.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets the name of the CloudEvent attribute type used to parse and format the value.
    /// </summary>
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
