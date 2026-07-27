//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Hermodr
{
    /// <summary>
    /// Specifies the serialisation format used by the OpenAPI writers.
    /// </summary>
    public enum OpenApiFormat
    {
        /// <summary>Serialise the OpenAPI document as JSON (default).</summary>
        Json,
        /// <summary>Serialise the OpenAPI document as YAML.</summary>
        Yaml
    }

    /// <summary>
    /// Extension methods that convert <see cref="IEventSchema"/> objects into
    /// <see cref="OpenApiSchema"/> instances suitable for embedding in an
    /// OpenAPI 3.1 document.
    /// </summary>
    public static class EventSchemaOpenApiExtensions
    {
        /// <summary>
        /// Converts an <see cref="IEventSchema"/> into an <see cref="OpenApiSchema"/>
        /// describing the event payload.
        /// </summary>
        public static OpenApiSchema ToOpenApiSchema(this IEventSchema schema)
        {
            var openSchema = new OpenApiSchema
            {
                Title = schema.EventType,
                Description = schema.Description,
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal),
                Required = new HashSet<string>(StringComparer.Ordinal)
            };

            foreach (var property in schema.Properties)
            {
                openSchema.Properties[property.Name] = property.ToOpenApiSchemaProperty();
                if (property.IsRequired)
                    openSchema.Required.Add(property.Name);
            }

            return openSchema;
        }

        /// <summary>
        /// Converts an <see cref="IEventProperty"/> into an <see cref="OpenApiSchema"/>
        /// representing a single property.
        /// </summary>
        public static OpenApiSchema ToOpenApiSchemaProperty(this IEventProperty property)
        {
            var (objType, format) = MapDataType(property.DataType);

            var prop = new OpenApiSchema
            {
                Type = objType,
                Format = format,
                Description = property.Description,
                Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal),
                Required = new HashSet<string>(StringComparer.Ordinal),
                Enum = new List<JsonNode>()
            };

            // Array items
            if (objType == JsonSchemaType.Array)
            {
                var elementDataType = property.DataType.EndsWith("[]", StringComparison.Ordinal)
                    ? property.DataType[..^2]
                    : "string";
                var (itemType, itemFormat) = MapDataType(elementDataType);
                prop.Items = new OpenApiSchema { Type = itemType, Format = itemFormat };
            }

            // Constraints
            foreach (var constraint in property.Constraints)
                ApplyConstraint(prop, constraint);

            // Nested object properties (skip if we already determined this is an array)
            if (property.Properties.Count > 0 && objType != JsonSchemaType.Array)
            {
                prop.Type = JsonSchemaType.Object;
                foreach (var sub in property.Properties)
                {
                    prop.Properties[sub.Name] = sub.ToOpenApiSchemaProperty();
                    if (sub.IsRequired)
                        prop.Required.Add(sub.Name);
                }
            }

            return prop;
        }

        // ─── Internal helpers ─────────────────────────────────────────────────────

        private static void ApplyConstraint(OpenApiSchema prop, IEventPropertyConstraint constraint)
        {
            switch (constraint.ConstraintType)
            {
                case "required":
                    // handled at parent level via Required set
                    break;

                case "allowedValues" when constraint is IEnumMemberConstraint enumConstraint:
                    foreach (var value in enumConstraint.AllowedValueObjects)
                        prop.Enum.Add(System.Text.Json.JsonSerializer.SerializeToNode(value));
                    break;

                case "range" when constraint is IRangeConstraint rangeConstraint:
                    if (rangeConstraint.Min is not null)
                        prop.Minimum = ConvertToDecimal(rangeConstraint.Min)?.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (rangeConstraint.Max is not null)
                        prop.Maximum = ConvertToDecimal(rangeConstraint.Max)?.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
            }
        }

        private static decimal? ConvertToDecimal(object? value)
        {
            if (value == null) return null;
            try { return Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture); }
            catch (FormatException) { return null; }
            catch (InvalidCastException) { return null; }
            catch (OverflowException) { return null; }
        }

        /// <summary>
        /// Maps the internal data-type string to a <see cref="JsonSchemaType"/> and
        /// an optional OpenAPI <c>format</c> string.
        /// </summary>
        internal static (JsonSchemaType type, string? format) MapDataType(string dataType)
        {
            if (dataType.EndsWith("[]", StringComparison.Ordinal))
                return (JsonSchemaType.Array, null);

            return dataType switch
            {
                "string"         => (JsonSchemaType.String, null),
                "int"            => (JsonSchemaType.Integer, "int32"),
                "long"           => (JsonSchemaType.Integer, "int64"),
                "float"          => (JsonSchemaType.Number, "float"),
                "double"         => (JsonSchemaType.Number, "double"),
                "money"          => (JsonSchemaType.Number, "decimal"),
                "boolean"        => (JsonSchemaType.Boolean, null),
                "dateTime"       => (JsonSchemaType.String, "date-time"),
                "dateTimeOffset" => (JsonSchemaType.String, "date-time"),
                "date"           => (JsonSchemaType.String, "date"),
                "time"           => (JsonSchemaType.String, "time"),
                "duration"       => (JsonSchemaType.String, "duration"),
                "guid"           => (JsonSchemaType.String, "uuid"),
                _                => (JsonSchemaType.Object, null)
            };
        }
    }
}