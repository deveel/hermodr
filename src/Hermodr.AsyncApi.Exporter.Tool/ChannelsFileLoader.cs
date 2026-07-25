//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

namespace Hermodr.Tool;

/// <summary>
/// Loads channel metadata from a JSON file with the schema:
/// <code>
/// { "channels": [
///   { "name": "orders", "transport": "rabbitmq",
///     "properties": { "exchange": "orders.x", "routingKey": "#" } }
/// ] }
/// </code>
/// </summary>
internal static class ChannelsFileLoader
{
    public static async Task<IReadOnlyList<IEventPublishChannelMetadata>> LoadAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        await using var stream = File.OpenRead(path);
        var doc = await JsonDocument.ParseAsync(stream);

        if (!doc.RootElement.TryGetProperty("channels", out var channelsEl))
            return Array.Empty<IEventPublishChannelMetadata>();

        var result = new List<IEventPublishChannelMetadata>();
        foreach (var entry in channelsEl.EnumerateArray())
        {
            var name = entry.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString()
                : null;
            var transport = entry.TryGetProperty("transport", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? EventTransports.Other
                : EventTransports.Other;

            var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (entry.TryGetProperty("properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in propsEl.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        properties[prop.Name] = prop.Value.GetString();
                    else if (prop.Value.ValueKind == JsonValueKind.Null)
                        properties[prop.Name] = null;
                }
            }

            if (string.Equals(transport, EventTransports.Other, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"warning: channel '{name}' has transport '{EventTransports.Other}'; skipping (not a message transport).");
                continue;
            }

            result.Add(new EventPublishChannelMetadata(name, transport, properties));
        }

        return result;
    }
}