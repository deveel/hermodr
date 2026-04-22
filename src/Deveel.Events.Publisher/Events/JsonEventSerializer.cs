//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Deveel.Events
{
    /// <summary>
    /// Serializes <see cref="CloudEvent"/> instances as plain
    /// <c>application/json</c> messages — the format most commonly expected
    /// by consumers of event-driven channels.
    /// </summary>
    /// <remarks>
    /// Each event is rendered as a flat JSON object whose top-level properties
    /// directly mirror the CloudEvent context attributes
    /// (<c>id</c>, <c>type</c>, <c>source</c>, <c>time</c>, …) together with a
    /// <c>data</c> property that holds the event payload.
    /// When <c>data</c> is already a JSON string it is embedded as a native
    /// JSON value rather than a quoted string.
    /// <br/>
    /// Batch deliveries produce a JSON <b>array</b> of the same objects.
    /// </remarks>
    public class JsonEventSerializer : IEventSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions =
            new() { WriteIndented = false };

        private static readonly HashSet<string> StandardAttributes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "specversion", "id", "type", "source", "time",
                "datacontenttype", "dataschema", "subject", "data"
            };

        /// <summary>A shared singleton instance.</summary>
        public static readonly JsonEventSerializer Default = new();

        /// <inheritdoc/>
        public string Format => EventMessageFormat.Json;

        /// <inheritdoc/>
        public string ContentType => "application/json; charset=utf-8";

        /// <inheritdoc/>
        public string BatchContentType => "application/json; charset=utf-8";

        /// <inheritdoc/>
        public byte[] Serialize(CloudEvent @event)
            => JsonSerializer.SerializeToUtf8Bytes(BuildNode(@event), SerializerOptions);

        /// <inheritdoc/>
        public byte[] SerializeBatch(IReadOnlyList<CloudEvent> events)
        {
            var array = new JsonArray();
            foreach (var e in events)
                array.Add(BuildNode(e));
            return JsonSerializer.SerializeToUtf8Bytes(array, SerializerOptions);
        }

        private static JsonObject BuildNode(CloudEvent @event)
        {
            var obj = new JsonObject();

            // Core context attributes
            if (@event.Id != null)          obj["id"]            = @event.Id;
            if (@event.Type != null)        obj["type"]          = @event.Type;
            if (@event.Source != null)      obj["source"]        = @event.Source.ToString();
            obj["specversion"] = @event.SpecVersion.VersionId;
            if (@event.Time.HasValue)
                obj["time"] = @event.Time.Value.ToString("O");
            if (@event.DataContentType != null) obj["datacontenttype"] = @event.DataContentType;
            if (@event.DataSchema != null)      obj["dataschema"]      = @event.DataSchema.ToString();
            if (@event.Subject != null)         obj["subject"]         = @event.Subject;

            // Extension attributes
            foreach (var (attr, value) in @event.GetPopulatedAttributes()
                .Where(a => !StandardAttributes.Contains(a.Key.Name)))
            {
                obj[attr.Name] = JsonValue.Create(value?.ToString());
            }

            // Data — try to embed raw JSON; fall back to string
            if (@event.Data is string dataStr)
            {
                try   { obj["data"] = JsonNode.Parse(dataStr); }
                catch { obj["data"] = dataStr; }
            }
            else if (@event.Data != null)
            {
                obj["data"] = JsonValue.Create(@event.Data.ToString());
            }

            return obj;
        }
    }
}

