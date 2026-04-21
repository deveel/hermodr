//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Newtonsoft.Json;

using Saunter.AsyncApiSchema.v2;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Deveel.Events {
    /// <summary>
    /// Specifies the serialisation format used by <see cref="EventSchemaAsyncApiWriter"/>
    /// and <see cref="EventSchemasAsyncApiWriter"/>.
    /// </summary>
    public enum AsyncApiFormat {
        /// <summary>Serialise the AsyncAPI document as JSON (default).</summary>
        Json,
        /// <summary>Serialise the AsyncAPI document as YAML.</summary>
        Yaml
    }

    /// <summary>
    /// Shared serialisation helpers for Saunter <see cref="AsyncApiDocument"/> objects.
    /// </summary>
    internal static class AsyncApiSerializer {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        private static readonly ISerializer YamlSerializer =
            new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

        private static readonly IDeserializer YamlJsonBridge =
            new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

        public static string ToJson(AsyncApiDocument document)
            => JsonConvert.SerializeObject(document, JsonSettings);

        public static string ToYaml(AsyncApiDocument document) {
            var json = ToJson(document);
            // Round-trip via YamlDotNet: deserialise the JSON object graph, then re-serialise as YAML
            var obj = YamlJsonBridge.Deserialize<object>(
                new System.IO.StringReader(json));
            return YamlSerializer.Serialize(obj);
        }

        public static string Serialize(AsyncApiDocument document, AsyncApiFormat format)
            => format == AsyncApiFormat.Yaml ? ToYaml(document) : ToJson(document);
    }
}
