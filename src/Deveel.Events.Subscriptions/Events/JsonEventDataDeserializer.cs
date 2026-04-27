//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Built-in fallback implementation of <see cref="IEventDataDeserializer"/> that handles
/// JSON content types by inspecting the event data without requiring a DI registration.
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
internal sealed class JsonEventDataDeserializer : IEventDataDeserializer
{
    /// <summary>
    /// Shared singleton instance used as the context fallback.
    /// </summary>
    internal static readonly JsonEventDataDeserializer Instance = new();

    /// <inheritdoc/>
    public bool CanDeserialize(string? contentType)
        => contentType is not null &&
           contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool TryDeserialize(CloudEvent @event, out JsonElement element)
        => TryGetJsonElement(@event, out element);

    // ── Static helpers (kept for any existing internal callers) ──────────────

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
