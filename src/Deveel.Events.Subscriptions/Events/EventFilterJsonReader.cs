//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Globalization;
using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// Deserializes an <see cref="EventFilter"/> from a <see cref="JsonElement"/> produced by
    /// <see cref="EventFilterJsonWriter"/>.
    /// </summary>
    internal static class EventFilterJsonReader
    {
        // ── Entry point ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads the filter represented by <paramref name="element"/> and returns the
        /// corresponding <see cref="EventFilter"/> instance.
        /// </summary>
        /// <exception cref="JsonException">
        /// The element does not contain a recognised <c>"filter"</c> discriminator.
        /// </exception>
        internal static EventFilter Read(JsonElement element)
        {
            if (!element.TryGetProperty("$filter", out var discriminatorEl))
                throw new JsonException("Missing required '$filter' discriminator property.");

            var discriminator = discriminatorEl.GetString()
                ?? throw new JsonException("The 'filter' discriminator must be a non-null string.");

            return discriminator switch
            {
                "attribute" => ReadAttribute(element),
                "data"      => ReadData(element),
                "and"       => ReadLogical(element, LogicalFilterOperator.And),
                "or"        => ReadLogical(element, LogicalFilterOperator.Or),
                _           => throw new JsonException($"Unknown filter discriminator '{discriminator}'.")
            };
        }

        // ── Per-type readers ──────────────────────────────────────────────────────────

        private static EventAttributeFilter ReadAttribute(JsonElement el)
        {
            var attribute = RequireString(el, "attribute");
            var value     = RequireString(el, "value");
            var matchMode = Enum.Parse<FilterMatchMode>(RequireString(el, "matchMode"));
            return new EventAttributeFilter(attribute, value, matchMode);
        }

        private static EventDataFilter ReadData(JsonElement el)
        {
            var path      = RequireString(el, "path");
            var op        = Enum.Parse<FilterOperator>(RequireString(el, "operator"));
            var valueType = RequireString(el, "valueType");

            object? value = valueType switch
            {
                "null"          => null,
                "bool"          => el.GetProperty("value").GetBoolean(),
                "int"           => el.GetProperty("value").GetInt32(),
                "long"          => el.GetProperty("value").GetInt64(),
                "double"        => el.GetProperty("value").GetDouble(),
                "string"        => el.GetProperty("value").GetString(),
                "datetime"      => DateTime.Parse(
                                       el.GetProperty("value").GetString()!,
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.RoundtripKind),
                "datetimeoffset" => DateTimeOffset.Parse(
                                       el.GetProperty("value").GetString()!,
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.RoundtripKind),
                _ => throw new JsonException($"Unknown valueType '{valueType}'.")
            };

            return new EventDataFilter(path, op, value);
        }

        private static LogicalEventFilter ReadLogical(JsonElement el, LogicalFilterOperator kind)
        {
            if (!el.TryGetProperty("filters", out var filtersEl) ||
                filtersEl.ValueKind != JsonValueKind.Array)
                throw new JsonException($"Expected a 'filters' array in logical filter.");

            var children = filtersEl.EnumerateArray()
                .Select(Read)
                .ToList();

            return new LogicalEventFilter(kind, children);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static string RequireString(JsonElement el, string propertyName)
        {
            if (!el.TryGetProperty(propertyName, out var prop))
                throw new JsonException($"Missing required property '{propertyName}'.");

            return prop.GetString()
                ?? throw new JsonException($"Property '{propertyName}' must be a non-null string.");
        }
    }
}


