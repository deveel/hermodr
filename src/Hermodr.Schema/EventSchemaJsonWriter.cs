//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

namespace Hermodr {
    /// <summary>
    /// An implementation of <see cref="IEventSchemaWriter"/> that writes
	/// the schema of an event to a JSON stream.
    /// </summary>
    public sealed class EventSchemaJsonWriter : IEventSchemaWriter {
        /// <summary>
        /// Constructs the writer with the given options to configure
		/// the JSON writer.
        /// </summary>
        /// <param name="jsonWriterOptions">
        /// Optional options to configure the underlying <see cref="System.Text.Json.Utf8JsonWriter"/>.
        /// When <c>null</c>, default <see cref="System.Text.Json.JsonWriterOptions"/> are used.
        /// </param>
        public EventSchemaJsonWriter(JsonWriterOptions? jsonWriterOptions = null) {
			JsonWriterOptions = jsonWriterOptions ?? new JsonWriterOptions();
		}

        /// <summary>
        /// The options that are used to configure the JSON writer.
        /// </summary>
        public JsonWriterOptions JsonWriterOptions { get; }

        /// <inheritdoc/>
        public async Task WriteToAsync(Stream stream, IEventSchema schema, CancellationToken cancellationToken = default) {
			using var writer = new Utf8JsonWriter(stream, JsonWriterOptions);
			writer.WriteStartObject();

			writer.WriteString("type", schema.EventType);
			writer.WriteString("version", schema.Version);
			writer.WriteString("contentType", schema.ContentType);

			if (schema.Description != null)
				writer.WriteString("description", schema.Description);

			writer.WritePropertyName("properties");
			writer.WriteStartObject();
			foreach (var property in schema.Properties)
				WriteProperty(writer, property);
			writer.WriteEndObject();

			writer.WriteEndObject();
			await writer.FlushAsync(cancellationToken);
		}

		private static void WriteProperty(Utf8JsonWriter writer, IEventProperty property) {
			writer.WritePropertyName(property.Name);
			writer.WriteStartObject();

			writer.WriteString("dataType", property.DataType);

			if (property.Version != null)
				writer.WriteString("version", property.Version);

			if (property.Description != null)
				writer.WriteString("description", property.Description);

			if (property.IsNullable)
				writer.WriteBoolean("nullable", true);

			foreach (var constraint in property.Constraints)
				WriteConstraint(writer, constraint);

			if (property.Properties.Count > 0) {
				writer.WritePropertyName("properties");
				writer.WriteStartObject();
				foreach (var subProperty in property.Properties)
					WriteProperty(writer, subProperty);
				writer.WriteEndObject();
			}

			writer.WriteEndObject();
		}

		private static void WriteConstraint(Utf8JsonWriter writer, IEventPropertyConstraint constraint) {
			switch (constraint.ConstraintType) {
				case "required":
					writer.WriteBoolean("required", true);
					break;

				case "allowedValues":
					WriteAllowedValuesConstraint(writer, constraint);
					break;

				case "range":
					WriteRangeConstraint(writer, constraint);
					break;
			}
		}

		private static void WriteAllowedValuesConstraint(Utf8JsonWriter writer, IEventPropertyConstraint constraint) {
			// EnumMemberConstraint<T> exposes AllowedValues — access via the non-generic helper interface
			if (constraint is IEnumMemberConstraint enumConstraint) {
				writer.WritePropertyName("allowedValues");
				writer.WriteStartArray();
				foreach (var value in enumConstraint.AllowedValueObjects)
					writer.WriteStringValue((string?) value?.ToString());
				writer.WriteEndArray();
			}
		}

		private static void WriteRangeConstraint(Utf8JsonWriter writer, IEventPropertyConstraint constraint) {
			if (constraint is IRangeConstraint rangeConstraint) {
				if (rangeConstraint.Min != null)
					WriteNamedValue(writer, "min", rangeConstraint.ValueType, rangeConstraint.Min);
				if (rangeConstraint.Max != null)
					WriteNamedValue(writer, "max", rangeConstraint.ValueType, rangeConstraint.Max);
			}
		}

		private static void WriteNamedValue(Utf8JsonWriter writer, string name, Type valueType, object value) {
			writer.WritePropertyName(name);
			WriteValue(writer, valueType, value);
		}

		private static void WriteValue(Utf8JsonWriter writer, Type valueType, object? value) {
			if (value == null) { writer.WriteNullValue(); return; }
			if (valueType == typeof(int))     { writer.WriteNumberValue((int)    value); return; }
			if (valueType == typeof(long))    { writer.WriteNumberValue((long)   value); return; }
			if (valueType == typeof(float))   { writer.WriteNumberValue((float)  value); return; }
			if (valueType == typeof(double))  { writer.WriteNumberValue((double) value); return; }
			if (valueType == typeof(decimal)) { writer.WriteNumberValue((decimal)value); return; }
			writer.WriteStringValue(value.ToString());
		}
	}
}
