//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using NJsonSchema;

using Saunter.AsyncApiSchema.v2;

namespace Hermodr {
    /// <summary>
    /// Provides extension methods to convert event schema objects into
    /// Saunter/AsyncAPI model objects (<see cref="JsonSchema"/>, <see cref="Message"/>,
    /// <see cref="AsyncApiDocument"/>).
    /// </summary>
    public static class EventSchemaAsyncApiExtensions {
        // ─── IEventSchema → NJsonSchema ───────────────────────────────────────────

        /// <summary>
        /// Converts an <see cref="IEventSchema"/> into an <see cref="JsonSchema"/>
        /// (NJsonSchema) that can be embedded in or referenced from an AsyncAPI document.
        /// </summary>
        public static JsonSchema ToJsonSchema(this IEventSchema schema) {
            var jsonSchema = new JsonSchema {
                Type = JsonObjectType.Object,
                Title = schema.EventType,
                Description = schema.Description
            };

            foreach (var property in schema.Properties) {
                var prop = property.ToJsonSchemaProperty();
                jsonSchema.Properties[property.Name] = prop;

                if (property.IsRequired)
                    jsonSchema.RequiredProperties.Add(property.Name);
            }

            return jsonSchema;
        }

        /// <summary>
        /// Converts an <see cref="IEventProperty"/> into a <see cref="JsonSchemaProperty"/>.
        /// </summary>
        public static JsonSchemaProperty ToJsonSchemaProperty(this IEventProperty property) {
            var (objType, format) = MapDataType(property.DataType);

            var prop = new JsonSchemaProperty {
                Type = objType,
                Format = format,
                Description = property.Description,
                IsNullableRaw = property.IsNullable ? true : null,
                IsRequired = property.IsRequired
            };

            // Array items
            if (objType == JsonObjectType.Array) {
                var elementDataType = property.DataType.EndsWith("[]", StringComparison.Ordinal)
                    ? property.DataType[..^2]
                    : "string";
                var (itemType, itemFormat) = MapDataType(elementDataType);
                prop.Item = new JsonSchema { Type = itemType, Format = itemFormat };
            }

            // Constraints
            foreach (var constraint in property.Constraints)
                ApplyConstraint(prop, constraint);

            // Nested object properties (skip if we already determined this is an array)
            if (property.Properties.Count > 0 && objType != JsonObjectType.Array) {
                prop.Type = JsonObjectType.Object;
                foreach (var sub in property.Properties) {
                    var subProp = sub.ToJsonSchemaProperty();
                    prop.Properties[sub.Name] = subProp;
                    if (sub.IsRequired)
                        prop.RequiredProperties.Add(sub.Name);
                }
            }

            return prop;
        }

        // ─── IEventSchema → Message ───────────────────────────────────────────────

        /// <summary>
        /// Converts an <see cref="IEventSchema"/> into a Saunter <see cref="Message"/>
        /// whose payload is built from the event schema.
        /// </summary>
        public static Message ToAsyncApiMessage(this IEventSchema schema) {
            return new Message {
                Name = schema.EventType,
                Title = schema.EventType,
                Summary = schema.Description,
                ContentType = schema.ContentType,
                Payload = schema.ToJsonSchema()
            };
        }

        // ─── IEventSchema → AsyncApiDocument ─────────────────────────────────────

        /// <summary>
        /// Builds a standalone <see cref="AsyncApiDocument"/> containing the given
        /// schema as a component and a channel that subscribes to the corresponding message.
        /// </summary>
        /// <param name="schema">The event schema to include in the document.</param>
        /// <param name="title">
        /// An optional title for the <c>info</c> block. Defaults to the event type.
        /// </param>
        /// <param name="version">
        /// An optional document version for the <c>info</c> block. Defaults to the
        /// schema version.
        /// </param>
        public static AsyncApiDocument ToAsyncApiDocument(
            this IEventSchema schema,
            string? title = null,
            string? version = null) {
            var doc = new AsyncApiDocument {
                Info = new Info(title ?? schema.EventType, version ?? schema.Version)
            };

            doc.AddSchema(schema);
            return doc;
        }

        // ─── Add to existing document ─────────────────────────────────────────────

        /// <summary>
        /// Adds the <paramref name="schema"/> as a named component schema + message and
        /// wires a subscribe channel into an existing <paramref name="document"/>.
        /// </summary>
        public static AsyncApiDocument AddSchema(
            this AsyncApiDocument document,
            IEventSchema schema) {
            var key = SanitizeKey(schema.EventType);

            document.Components ??= new Components();
            document.Components.Schemas[key] = schema.ToJsonSchema();
            document.Components.Messages[key] = schema.ToAsyncApiMessage();

            document.Channels[key] = new ChannelItem {
                Description = schema.Description,
                Subscribe = new Operation {
                    Summary = schema.Description,
                    Message = new MessageReference(key)
                }
            };

            return document;
        }

        // ─── Internal helpers ─────────────────────────────────────────────────────

        private static void ApplyConstraint(JsonSchemaProperty prop, IEventPropertyConstraint constraint) {
            switch (constraint.ConstraintType) {
                case "required":
                    // handled at parent level via IsRequired
                    break;

                case "allowedValues" when constraint is IEnumMemberConstraint enumConstraint:
                    foreach (var value in enumConstraint.AllowedValueObjects)
                        prop.Enumeration.Add(value?.ToString());
                    break;

                case "range" when constraint is IRangeConstraint rangeConstraint:
                    if (rangeConstraint.Min is not null)
                        prop.Minimum = ConvertToDecimal(rangeConstraint.Min);
                    if (rangeConstraint.Max is not null)
                        prop.Maximum = ConvertToDecimal(rangeConstraint.Max);
                    break;
            }
        }

        private static decimal? ConvertToDecimal(object? value) {
            if (value == null) return null;
            try { return Convert.ToDecimal(value); } catch { return null; }
        }

        /// <summary>
        /// Maps the internal data-type string to an NJsonSchema object type and
        /// optional format string.
        /// </summary>
        internal static (JsonObjectType type, string? format) MapDataType(string dataType) {
            if (dataType.EndsWith("[]", StringComparison.Ordinal))
                return (JsonObjectType.Array, null);

            return dataType switch {
                "string"         => (JsonObjectType.String, null),
                "int"            => (JsonObjectType.Integer, "int32"),
                "long"           => (JsonObjectType.Integer, "int64"),
                "float"          => (JsonObjectType.Number, "float"),
                "double"         => (JsonObjectType.Number, "double"),
                "money"          => (JsonObjectType.Number, "decimal"),
                "boolean"        => (JsonObjectType.Boolean, null),
                "dateTime"       => (JsonObjectType.String, "date-time"),
                "dateTimeOffset" => (JsonObjectType.String, "date-time"),
                "date"           => (JsonObjectType.String, "date"),
                "time"           => (JsonObjectType.String, "time"),
                "duration"       => (JsonObjectType.String, "duration"),
                "guid"           => (JsonObjectType.String, "uuid"),
                _                => (JsonObjectType.Object, null)
            };
        }

        private static string SanitizeKey(string eventType)
            => eventType.Replace('.', '-').Replace('/', '-').Replace(' ', '-');
    }
}

