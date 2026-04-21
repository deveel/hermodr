//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text;

using YamlDotNet.Serialization;

namespace Deveel.Events {
    /// <summary>
    /// An implementation of <see cref="IEventSchemaWriter"/> that writes
    /// the schema of an event to a YAML stream.
    /// </summary>
    public sealed class EventSchemaYamlWriter : IEventSchemaWriter {
        private readonly ISerializer _serializer;

        /// <summary>
        /// Constructs the writer using the default YAML serializer settings.
        /// </summary>
        public EventSchemaYamlWriter()
            : this(new SerializerBuilder().WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance).Build()) {
        }

        /// <summary>
        /// Constructs the writer with a pre-configured <see cref="ISerializer"/>.
        /// </summary>
        /// <param name="serializer">The YAML serializer to use.</param>
        public EventSchemaYamlWriter(ISerializer serializer) {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <inheritdoc/>
        public async Task WriteToAsync(Stream stream, IEventSchema schema, CancellationToken cancellationToken = default) {
            var document = BuildDocument(schema);
            var yaml = _serializer.Serialize(document);
            var bytes = Encoding.UTF8.GetBytes(yaml);
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }

        private static Dictionary<string, object?> BuildDocument(IEventSchema schema) {
            var doc = new Dictionary<string, object?> {
                ["type"] = schema.EventType,
                ["version"] = schema.Version,
                ["contentType"] = schema.ContentType
            };

            if (schema.Description != null)
                doc["description"] = schema.Description;

            var properties = new Dictionary<string, object?>();
            foreach (var property in schema.Properties)
                properties[property.Name] = BuildProperty(property);

            doc["properties"] = properties;
            return doc;
        }

        private static Dictionary<string, object?> BuildProperty(IEventProperty property) {
            var map = new Dictionary<string, object?> {
                ["dataType"] = property.DataType
            };

            if (property.Version != null)
                map["version"] = property.Version;

            if (property.Description != null)
                map["description"] = property.Description;

            if (property.IsNullable)
                map["nullable"] = true;

            foreach (var constraint in property.Constraints)
                ApplyConstraint(map, constraint);

            if (property.Properties.Count > 0) {
                var nested = new Dictionary<string, object?>();
                foreach (var sub in property.Properties)
                    nested[sub.Name] = BuildProperty(sub);
                map["properties"] = nested;
            }

            return map;
        }

        private static void ApplyConstraint(Dictionary<string, object?> map, IEventPropertyConstraint constraint) {
            switch (constraint.ConstraintType) {
                case "required":
                    map["required"] = true;
                    break;

                case "allowedValues":
                    if (constraint is IEnumMemberConstraint enumConstraint)
                        map["allowedValues"] = enumConstraint.AllowedValueObjects
                            .Select(v => v?.ToString())
                            .ToList();
                    break;

                case "range":
                    if (constraint is IRangeConstraint rangeConstraint) {
                        if (rangeConstraint.Min != null)
                            map["min"] = rangeConstraint.Min;
                        if (rangeConstraint.Max != null)
                            map["max"] = rangeConstraint.Max;
                    }
                    break;
            }
        }
    }
}
