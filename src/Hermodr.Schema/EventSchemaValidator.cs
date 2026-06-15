//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Hermodr
{
    /// <summary>
    /// Validates CloudEvent payloads against their registered schemas using format-agnostic deserialization.
    /// </summary>
    public sealed class EventSchemaValidator : IEventSchemaValidator
    {
        private readonly IEventDataDeserializerRegistry _deserializerRegistry;
        private readonly ILogger<EventSchemaValidator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSchemaValidator"/> class.
        /// </summary>
        /// <param name="deserializerRegistry">The deserializer registry for content-type-based deserialization.</param>
        /// <param name="logger">A logger instance for diagnostic output.</param>
        public EventSchemaValidator(
            IEventDataDeserializerRegistry deserializerRegistry,
            ILogger<EventSchemaValidator> logger)
        {
            _deserializerRegistry = deserializerRegistry;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<ValidationResult> ValidateEventAsync(
            IEventSchema schema,
            CloudEvent @event,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(@event);

            var contentType = @event.DataContentType ?? "application/json";
            var deserializer = _deserializerRegistry.GetDeserializer(contentType);

            if (deserializer == null)
            {
                _logger.LogWarning(
                    "No deserializer found for content type {ContentType}, skipping validation for event type {EventType}",
                    contentType,
                    @event.Type);
                yield break;
            }

            var dataStream = GetDataStream(@event);
            if (dataStream == null || dataStream.Length == 0)
            {
                yield break;
            }

            object? data;
            try
            {
                data = await deserializer.DeserializeAsync(dataStream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize event data for event type {EventType}", @event.Type);
                yield break;
            }

            foreach (var error in ValidateObjectTree(data, schema, pathPrefix: ""))
            {
                yield return error;
            }
        }

        private static Stream? GetDataStream(CloudEvent @event)
        {
            if (@event.Data == null)
                return null;

            if (@event.Data is Stream stream)
                return stream;

            if (@event.Data is byte[] bytes)
                return new MemoryStream(bytes);

            if (@event.Data is string str)
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(str));

            return null;
        }

        private IEnumerable<ValidationResult> ValidateObjectTree(
            object? data,
            IEventSchema schema,
            string pathPrefix)
        {
            if (data == null)
            {
                yield return new ValidationResult(
                    "Event data is null",
                    memberNames: Array.Empty<string>());
                yield break;
            }

            var dataDict = ConvertToDictionary(data);

            foreach (var property in schema.Properties)
            {
                var propertyPath = string.IsNullOrEmpty(pathPrefix)
                    ? property.Name
                    : $"{pathPrefix}.{property.Name}";

                if (!dataDict.TryGetValue(property.Name, out var value))
                {
                    if (property.IsRequired)
                    {
                        yield return new ValidationResult(
                            $"Required property '{property.Name}' is missing",
                            memberNames: new[] { propertyPath });
                    }
                    continue;
                }

                if (value == null && !property.IsNullable)
                {
                    yield return new ValidationResult(
                        $"Property '{property.Name}' cannot be null",
                        memberNames: new[] { propertyPath });
                    continue;
                }

                if (value == null)
                    continue;

                foreach (var constraint in property.Constraints)
                {
                    var error = ValidateConstraint(value, constraint, propertyPath);
                    if (error != null)
                        yield return error;
                }

                if (property.Properties.Count > 0)
                {
                    var nestedSchema = CreateNestedSchema(property);
                    foreach (var nestedError in ValidateObjectTree(value, nestedSchema, propertyPath))
                    {
                        yield return nestedError;
                    }
                }
            }
        }

        private static Dictionary<string, object?> ConvertToDictionary(object? data)
        {
            if (data == null)
                return new Dictionary<string, object?>();

            if (data is Dictionary<string, object?> dict)
                return dict;

            if (data is IDictionary<string, object?> genericDict)
                return new Dictionary<string, object?>(genericDict);

            if (data is IDictionary nonGenericDict)
            {
                var result = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in nonGenericDict)
                {
                    result[entry.Key.ToString() ?? string.Empty] = entry.Value;
                }
                return result;
            }

            return new Dictionary<string, object?>();
        }

        private static ValidationResult? ValidateConstraint(
            object value,
            IEventPropertyConstraint constraint,
            string propertyPath)
        {
            return constraint switch
            {
                PropertyRequiredConstraint => ValidateRequired(value, propertyPath),
                IRangeConstraint range => ValidateRange(value, range, propertyPath),
                IEnumMemberConstraint enumConstraint => ValidateEnum(value, enumConstraint, propertyPath),
                _ => null
            };
        }

        private static ValidationResult? ValidateRequired(object value, string propertyPath)
        {
            if (value == null)
            {
                return new ValidationResult(
                    $"Required property '{GetPropertyName(propertyPath)}' is missing",
                    memberNames: new[] { propertyPath });
            }
            return null;
        }

        private static ValidationResult? ValidateRange(object value, IRangeConstraint constraint, string propertyPath)
        {
            if (value == null)
                return null;

            if (constraint.Min == null && constraint.Max == null)
                return null;

            // Convert numeric values to a common type for comparison
            // JSON deserialization produces double, but constraints may use int/long/decimal
            if (constraint.Min != null)
            {
                var normalizedValue = ConvertNumericValue(value, constraint.Min.GetType());
                if (normalizedValue != null && Comparer<object>.Default.Compare(normalizedValue, constraint.Min) < 0)
                {
                    return new ValidationResult(
                        $"Value '{value}' is below minimum {constraint.Min}",
                        memberNames: new[] { propertyPath });
                }
            }

            if (constraint.Max != null)
            {
                var normalizedValue = ConvertNumericValue(value, constraint.Max.GetType());
                if (normalizedValue != null && Comparer<object>.Default.Compare(normalizedValue, constraint.Max) > 0)
                {
                    return new ValidationResult(
                        $"Value '{value}' is above maximum {constraint.Max}",
                        memberNames: new[] { propertyPath });
                }
            }

            return null;
        }

        private static object? ConvertNumericValue(object value, Type targetType)
        {
            try
            {
                // If the types match, return as-is
                if (value.GetType() == targetType)
                    return value;

                // If both are numeric, try to convert
                if (value is double d)
                {
                    if (targetType == typeof(int)) return (int)d;
                    if (targetType == typeof(long)) return (long)d;
                    if (targetType == typeof(float)) return (float)d;
                    if (targetType == typeof(decimal)) return (decimal)d;
                    if (targetType == typeof(double)) return d;
                }
                if (value is int i)
                {
                    if (targetType == typeof(double)) return (double)i;
                    if (targetType == typeof(long)) return (long)i;
                    if (targetType == typeof(decimal)) return (decimal)i;
                }
                if (value is long l)
                {
                    if (targetType == typeof(double)) return (double)l;
                    if (targetType == typeof(int)) return (int)l;
                }

                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return null;
            }
        }

        private static ValidationResult? ValidateEnum(object value, IEnumMemberConstraint constraint, string propertyPath)
        {
            // Try direct match first
            if (constraint.AllowedValueObjects.Any(v => Equals(v, value)))
                return null;

            // Try numeric conversion for JSON number mismatches (double vs int)
            foreach (var allowed in constraint.AllowedValueObjects)
            {
                if (allowed == null) continue;

                // If both are numeric types, try converting
                if (IsNumericType(value.GetType()) && IsNumericType(allowed.GetType()))
                {
                    var converted = ConvertNumericValue(value, allowed.GetType());
                    if (converted != null && Equals(converted, allowed))
                        return null;
                }
            }

            var allowedValues = string.Join(", ", constraint.AllowedValueObjects.Select(v => v?.ToString() ?? "null"));
            return new ValidationResult(
                $"Value '{value}' is not in allowed set [{allowedValues}]",
                memberNames: new[] { propertyPath });
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int)
                || type == typeof(long)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal)
                || type == typeof(short)
                || type == typeof(byte);
        }

        private static string GetPropertyName(string propertyPath)
        {
            var parts = propertyPath.Split('.');
            return parts.Length > 0 ? parts[parts.Length - 1] : propertyPath;
        }

        private static IEventSchema CreateNestedSchema(IEventProperty property)
        {
            var nestedSchema = new EventSchema(
                $"nested.{property.Name}",
                property.Version?.ToString() ?? "1.0",
                "object")
            {
                Description = property.Description
            };

            foreach (var subProperty in property.Properties)
            {
                nestedSchema.Properties.Add((EventProperty)subProperty);
            }

            return nestedSchema;
        }
    }
}
