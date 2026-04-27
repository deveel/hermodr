//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// An <see cref="IEventDataDeserializer"/> that handles JSON-encoded cloud event payloads.
/// </summary>
/// <remarks>
/// <para>
/// Handles events whose <c>datacontenttype</c> contains <c>"json"</c>
/// (case-insensitive), such as <c>application/json</c> or
/// <c>application/cloudevents+json</c>.
/// </para>
/// <para>
/// Payload resolution order inside <see cref="TryDeserialize{T}"/>:
/// <list type="number">
///   <item><description>
///     <c>event.Data</c> is already a <see cref="JsonElement"/> — used as-is.
///   </description></item>
///   <item><description>
///     <c>event.Data</c> is a <c>string</c> — parsed as a JSON document.
///   </description></item>
///   <item><description>
///     <c>event.Data</c> is any other non-binary object — serialized via
///     <see cref="JsonSerializer"/>, then parsed.
///   </description></item>
/// </list>
/// <c>byte[]</c>, <see cref="Stream"/>, and <c>null</c> payloads are not handled
/// by this deserializer; register a separate <see cref="IEventDataDeserializer"/>
/// for those formats.
/// </para>
/// </remarks>
public sealed class JsonEventDataDeserializer : IEventDataDeserializer
{
    private readonly JsonSerializerOptions? _options;

    /// <summary>
    /// Initialises the deserializer, optionally with custom
    /// <see cref="JsonSerializerOptions"/>.
    /// </summary>
    public JsonEventDataDeserializer(JsonSerializerOptions? options = null)
    {
        _options = options;
    }

    /// <summary>Gets the serializer options used by this instance (may be <c>null</c>).</summary>
    public JsonSerializerOptions? Options => _options;

    // ── IEventDataDeserializer ──────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <c>true</c> when <paramref name="contentType"/> is non-null and contains
    /// the substring <c>"json"</c> (case-insensitive).
    /// </remarks>
    public bool CanDeserialize(string? contentType)
        => contentType is not null &&
           contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool TryDeserialize<T>(CloudEvent @event, out T? result) where T : class
    {
        if (!TryGetJsonElement(@event, out var element))
        {
            result = null;
            return false;
        }

        try
        {
            result = element.Deserialize<T>(_options);
            return result is not null;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    // ── Shared JSON helpers (called by Json*DataFilter) ─────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the event's <c>datacontenttype</c> is JSON-compatible
    /// (the value contains <c>"json"</c>, case-insensitive).
    /// </summary>
    public static bool IsJsonContent(CloudEvent @event)
    {
        var ct = @event.DataContentType;
        return ct is not null && ct.Contains("json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to expose the event data as a <see cref="JsonElement"/>.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><description>
    ///     <c>event.Data</c> is already a <see cref="JsonElement"/> — used as-is.
    ///   </description></item>
    ///   <item><description>
    ///     <c>event.Data</c> is a <c>string</c> and content-type is JSON — parsed.
    ///   </description></item>
    ///   <item><description>
    ///     <c>event.Data</c> is any other non-binary object and content-type is JSON —
    ///     serialized with <see cref="JsonSerializer"/> then parsed.
    ///   </description></item>
    /// </list>
    /// Returns <c>false</c> when none of the above succeeds (including <c>byte[]</c>,
    /// <see cref="Stream"/>, and <c>null</c> payloads).
    /// </remarks>
    public static bool TryGetJsonElement(CloudEvent @event, out JsonElement element)
    {
        switch (@event.Data)
        {
            case JsonElement je:
                element = je;
                return true;

            case string s when IsJsonContent(@event):
                try
                {
                    using var doc = JsonDocument.Parse(s);
                    element = doc.RootElement.Clone();
                    return true;
                }
                catch { break; }

            case byte[] or Stream or null:
                break;

            default:
                if (IsJsonContent(@event))
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(@event.Data);
                        using var doc = JsonDocument.Parse(json);
                        element = doc.RootElement.Clone();
                        return true;
                    }
                    catch { /* fall through */ }
                }
                break;
        }

        element = default;
        return false;
    }
}

