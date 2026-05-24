//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr;

/// <summary>
/// Represents a single CloudEvent extension attribute persisted as a row in a
/// relational table, in a one-to-many relationship with <see cref="DbOutboxMessage"/>.
/// </summary>
/// <remarks>
/// <para>
/// The CloudEvents specification allows any number of named extension attributes
/// beyond the set of well-known context attributes (<c>id</c>, <c>type</c>,
/// <c>source</c>, <c>specversion</c>, <c>time</c>, <c>subject</c>,
/// <c>datacontenttype</c>, <c>dataschema</c>). Those well-known attributes are
/// mapped as scalar columns directly on <see cref="DbOutboxMessage"/>; extension
/// attributes are stored as rows in this table instead.
/// </para>
/// <para>
/// The <see cref="ValueType"/> column records the CloudEvents attribute-type name
/// so that the correct <see cref="CloudEventAttributeType"/> can be selected when
/// the parent <see cref="DbOutboxMessage"/> reconstructs its <see cref="CloudEvent"/>
/// via <see cref="DbOutboxMessage.BuildCloudEvent"/>.
/// Supported values are:
/// <list type="bullet">
///   <item><term><c>string</c></term><description>Default – <see cref="CloudEventAttributeType.String"/>.</description></item>
///   <item><term><c>integer</c></term><description><see cref="CloudEventAttributeType.Integer"/>.</description></item>
///   <item><term><c>boolean</c></term><description><see cref="CloudEventAttributeType.Boolean"/>.</description></item>
///   <item><term><c>uri</c></term><description><see cref="CloudEventAttributeType.Uri"/>.</description></item>
///   <item><term><c>urireference</c></term><description><see cref="CloudEventAttributeType.UriReference"/>.</description></item>
///   <item><term><c>timestamp</c></term><description><see cref="CloudEventAttributeType.Timestamp"/>.</description></item>
///   <item><term><c>binary</c></term><description><see cref="CloudEventAttributeType.Binary"/> – value stored as Base64.</description></item>
/// </list>
/// </para>
/// </remarks>
public class DbCloudEventAttribute
{
    /// <summary>
    /// Gets or sets the surrogate primary key (database-generated auto-increment).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the foreign key that links this attribute row to its parent
    /// <see cref="DbOutboxMessage"/>.
    /// </summary>
    public string MessageId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the navigation property back to the owning
    /// <see cref="DbOutboxMessage"/>.
    /// </summary>
    public DbOutboxMessage Message { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CloudEvents extension attribute name (lower-case, no hyphens,
    /// per the CloudEvents specification).
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the string-formatted value of the attribute as returned by
    /// <see cref="CloudEventAttributeType.Format"/>.
    /// Binary values are stored as Base64.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the CloudEvents attribute-type identifier used to select the
    /// correct <see cref="CloudEventAttributeType"/> when deserialising this attribute
    /// back into a <see cref="CloudEvent"/>.
    /// Defaults to <c>"string"</c>.
    /// </summary>
    public string ValueType { get; set; } = "string";

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the <see cref="CloudEventAttributeType"/> that corresponds to
    /// <see cref="ValueType"/>.
    /// </summary>
    internal CloudEventAttributeType GetAttributeType() => ValueType switch
    {
        "integer"      => CloudEventAttributeType.Integer,
        "boolean"      => CloudEventAttributeType.Boolean,
        "uri"          => CloudEventAttributeType.Uri,
        "urireference" => CloudEventAttributeType.UriReference,
        "timestamp"    => CloudEventAttributeType.Timestamp,
        "binary"       => CloudEventAttributeType.Binary,
        _              => CloudEventAttributeType.String
    };

    /// <summary>
    /// Applies this extension attribute to <paramref name="cloudEvent"/> using the
    /// stored <see cref="Name"/>, <see cref="Value"/>, and <see cref="ValueType"/>.
    /// </summary>
    /// <param name="cloudEvent">The target <see cref="CloudEvent"/> to modify.</param>
    internal void ApplyTo(CloudEvent cloudEvent)
    {
        if (Value is null) return;

        var attrType  = GetAttributeType();
        var attr      = CloudEventAttribute.CreateExtension(Name, attrType);
        cloudEvent[attr] = attrType.Parse(Value);
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="DbCloudEventAttribute"/> from a populated Event
    /// extension attribute key/value pair.
    /// </summary>
    /// <param name="messageId">The foreign-key value linking this row to the parent message.</param>
    /// <param name="attribute">The <see cref="CloudEventAttribute"/> descriptor.</param>
    /// <param name="value">The attribute value object (as returned by the CloudEvent indexer).</param>
    /// <returns>A new, unpersisted <see cref="DbCloudEventAttribute"/> instance.</returns>
    internal static DbCloudEventAttribute FromAttribute(
        string messageId,
        CloudEventAttribute attribute,
        object value)
    {
        var typeName = GetTypeName(attribute.Type);
        return new DbCloudEventAttribute
        {
            MessageId = messageId,
            Name      = attribute.Name,
            Value     = attribute.Type.Format(value),
            ValueType = typeName
        };
    }

    private static string GetTypeName(CloudEventAttributeType type)
    {
        if (type == CloudEventAttributeType.Integer)      return "integer";
        if (type == CloudEventAttributeType.Boolean)      return "boolean";
        if (type == CloudEventAttributeType.Uri)          return "uri";
        if (type == CloudEventAttributeType.UriReference) return "urireference";
        if (type == CloudEventAttributeType.Timestamp)    return "timestamp";
        if (type == CloudEventAttributeType.Binary)       return "binary";
        return "string";
    }
}

