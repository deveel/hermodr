//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

namespace Hermodr;

/// <summary>
/// Loads channel metadata from a JSON file with the schema:
/// <code>
/// { "channels": [
///   { "name": "orders", "transport": "rabbitmq",
///     "properties": { "exchange": "orders.x", "routingKey": "#" } }
/// ] }
/// </code>
/// </summary>
/// <remarks>
/// Channels whose <see cref="IEventPublishChannelMetadata.Transport"/> equals
/// <see cref="EventTransports.Other"/> are skipped (with a warning recorded in
/// the returned <see cref="ChannelMetadataFileLoadResult.Warnings"/>, so the
/// caller — not this loader — decides how to surface them).
/// </remarks>
public static class ChannelMetadataFileLoader
{
    /// <summary>
    /// Loads the channel metadata described by the JSON file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path to the JSON channels file.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous read operation.
    /// </param>
    /// <returns>
    /// A <see cref="ChannelMetadataFileLoadResult"/> carrying the loaded
    /// channels and any non-fatal warnings encountered while parsing the file
    /// (for example, a channel with <c>transport: "other"</c> that was
    /// skipped).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the file does not exist.
    /// </exception>
    /// <exception cref="JsonException">
    /// Thrown when the file is not valid JSON.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown on I/O failures.
    /// </exception>
    public static async Task<ChannelMetadataFileLoadResult> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        await using var stream = File.OpenRead(path);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var warns = new List<string>();

        if (!doc.RootElement.TryGetProperty("channels", out var channelsEl))
            return new ChannelMetadataFileLoadResult(Array.Empty<IEventPublishChannelMetadata>(), Array.Empty<string>());

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
                warns.Add($"channel '{name}' has transport '{EventTransports.Other}'; skipping (not a message transport).");
                continue;
            }

            result.Add(new EventPublishChannelMetadata(name, transport, properties));
        }

        return new ChannelMetadataFileLoadResult(result, warns);
    }
}

/// <summary>
/// The result of <see cref="ChannelMetadataFileLoader.LoadAsync"/>: the
/// resolved channels plus any non-fatal warnings produced while parsing the
/// file.
/// </summary>
public sealed class ChannelMetadataFileLoadResult
{
    /// <summary>
    /// Creates a new result.
    /// </summary>
    public ChannelMetadataFileLoadResult(
        IReadOnlyList<IEventPublishChannelMetadata> channels,
        IReadOnlyList<string> warnings)
    {
        Channels = channels;
        Warnings = warnings;
    }

    /// <summary>
    /// The loaded channel metadata (channels with
    /// <see cref="EventTransports.Other"/> transport are excluded).
    /// </summary>
    public IReadOnlyList<IEventPublishChannelMetadata> Channels { get; }

    /// <summary>
    /// Non-fatal warnings recorded while parsing the file.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }
}