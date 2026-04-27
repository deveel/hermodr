//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Globalization;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// An <see cref="IEventFilter"/> that navigates a dot-separated JSON path inside the event
    /// data payload and compares the resolved field value using a <see cref="FilterOperator"/>
    /// and a typed reference value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The event data is accessed via an internal JSON helper that supports JSON strings,
    /// already-parsed <see cref="JsonElement"/> objects, and any CLR object that can be
    /// serialized to JSON (e.g. anonymous types, generated records).
    /// Returns <c>false</c> silently when the data cannot be represented as JSON or the path
    /// cannot be traversed.
    /// </para>
    /// <para>
    /// Supported value types for comparison: <see cref="bool"/>, <see cref="int"/>,
    /// <see cref="long"/>, <see cref="double"/>, <see cref="string"/>,
    /// <see cref="DateTime"/>, and <see cref="DateTimeOffset"/>.
    /// </para>
    /// <para>
    /// For <see cref="FilterOperator.Exists"/> and <see cref="FilterOperator.NotExists"/>
    /// no reference value is needed; use <see cref="Exists"/> / <see cref="NotExists"/>.
    /// </para>
    /// </remarks>
    public sealed class EventDataFilter : IEventFilter
    {
        private readonly string[] _segments;

        private EventDataFilter(string path, FilterOperator @operator, object? value)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must not be empty.", nameof(path));

            Path = path;
            _segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            Operator = @operator;
            Value = value;
        }

        // ── Properties ──────────────────────────────────────────────────────────────

        /// <summary>Gets the dot-separated path used to locate the field in the JSON body.</summary>
        public string Path { get; }

        /// <summary>Gets the comparison operator applied to the resolved field value.</summary>
        public FilterOperator Operator { get; }

        /// <summary>
        /// Gets the reference value used in the comparison.
        /// Supported CLR types: <see cref="bool"/>, <see cref="int"/>, <see cref="long"/>,
        /// <see cref="double"/>, <see cref="string"/>, <see cref="DateTime"/>,
        /// <see cref="DateTimeOffset"/>, or <c>null</c> for existence checks.
        /// </summary>
        public object? Value { get; }

        // ── Factory methods ─────────────────────────────────────────────────────────

        /// <summary>Creates a filter that compares the field at <paramref name="path"/> with a <see cref="bool"/> value.</summary>
        public static EventDataFilter Create(string path, FilterOperator @operator, bool value)
            => new(path, @operator, value);

        /// <summary>Creates a filter that compares the field at <paramref name="path"/> with an <see cref="int"/> value.</summary>
        public static EventDataFilter Create(string path, FilterOperator @operator, int value)
            => new(path, @operator, value);

        /// <summary>Creates a filter that compares the field at <paramref name="path"/> with a <see cref="long"/> value.</summary>
        public static EventDataFilter Create(string path, FilterOperator @operator, long value)
            => new(path, @operator, value);

        /// <summary>Creates a filter that compares the field at <paramref name="path"/> with a <see cref="double"/> value.</summary>
        public static EventDataFilter Create(string path, FilterOperator @operator, double value)
            => new(path, @operator, value);

        /// <summary>Creates a filter that compares the field at <paramref name="path"/> with a <see cref="string"/> value.</summary>
        public static EventDataFilter Create(string path, FilterOperator @operator, string value)
            => new(path, @operator, value);

        /// <summary>Creates a filter that compares the field at <paramref name="path"/> with a <see cref="DateTime"/> value.</summary>
        public static EventDataFilter Create(string path, FilterOperator @operator, DateTime value)
            => new(path, @operator, value);

        /// <summary>Creates a filter that compares the field at <paramref name="path"/> with a <see cref="DateTimeOffset"/> value.</summary>
        public static EventDataFilter Create(string path, FilterOperator @operator, DateTimeOffset value)
            => new(path, @operator, value);

        /// <summary>
        /// Creates a filter that passes when the JSON property at <paramref name="path"/> exists
        /// (regardless of its value).
        /// </summary>
        public static EventDataFilter Exists(string path)
            => new(path, FilterOperator.Exists, null);

        /// <summary>
        /// Creates a filter that passes when the JSON property at <paramref name="path"/> is
        /// absent from the payload.
        /// </summary>
        public static EventDataFilter NotExists(string path)
            => new(path, FilterOperator.NotExists, null);

        // ── IEventFilter ─────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool Matches(CloudEvent @event, EventSubscriptionContext context)
        {
            if (@event is null)
                return false;

            var jsonData = context.GetJsonData(@event);
            if (jsonData is null)
                return false;

            var current = jsonData.Value;
            foreach (var seg in _segments)
            {
                if (current.ValueKind != JsonValueKind.Object)
                    return Operator == FilterOperator.NotExists;

                if (!current.TryGetProperty(seg, out current))
                    return Operator == FilterOperator.NotExists;
            }

            return Operator switch
            {
                FilterOperator.Exists    => true,
                FilterOperator.NotExists => false,
                _                        => CompareJsonElement(current)
            };
        }

        // ── Comparison helpers ────────────────────────────────────────────────────────

        private bool CompareJsonElement(JsonElement element) => Value switch
        {
            bool boolVal           => CompareBool(element, boolVal),
            int intVal             => CompareNumeric(element, (double)intVal),
            long longVal           => CompareNumeric(element, (double)longVal),
            double dblVal          => CompareNumeric(element, dblVal),
            string strVal          => CompareString(element, strVal),
            DateTime dtVal         => CompareDateTime(element, new DateTimeOffset(dtVal)),
            DateTimeOffset dtoVal  => CompareDateTime(element, dtoVal),
            null                   => CompareNull(element),
            _                      => false
        };

        private bool CompareBool(JsonElement el, bool refValue)
        {
            bool? elBool = el.ValueKind switch
            {
                JsonValueKind.True  => true,
                JsonValueKind.False => false,
                _                   => null
            };

            if (!elBool.HasValue) return false;

            return Operator switch
            {
                FilterOperator.Equals    => elBool.Value == refValue,
                FilterOperator.NotEquals => elBool.Value != refValue,
                _                        => false
            };
        }

        private bool CompareNumeric(JsonElement el, double refNum)
        {
            double elNum;
            if (el.ValueKind == JsonValueKind.Number)
                elNum = el.GetDouble();
            else if (!double.TryParse(el.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out elNum))
                return false;

            int cmp = elNum.CompareTo(refNum);
            return Operator switch
            {
                FilterOperator.Equals             => cmp == 0,
                FilterOperator.NotEquals          => cmp != 0,
                FilterOperator.GreaterThan         => cmp > 0,
                FilterOperator.LessThan            => cmp < 0,
                FilterOperator.GreaterThanOrEqual  => cmp >= 0,
                FilterOperator.LessThanOrEqual     => cmp <= 0,
                _                                  => false
            };
        }

        private bool CompareString(JsonElement el, string refValue)
        {
            var elStr = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
            if (elStr is null) return false;

            return Operator switch
            {
                FilterOperator.Equals    => string.Equals(elStr, refValue, StringComparison.Ordinal),
                FilterOperator.NotEquals => !string.Equals(elStr, refValue, StringComparison.Ordinal),
                FilterOperator.StartsWith => elStr.StartsWith(refValue, StringComparison.Ordinal),
                FilterOperator.EndsWith   => elStr.EndsWith(refValue, StringComparison.Ordinal),
                FilterOperator.Contains   => elStr.Contains(refValue, StringComparison.Ordinal),
                _                         => false
            };
        }

        private bool CompareDateTime(JsonElement el, DateTimeOffset refDto)
        {
            var str = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
            if (str is null || !DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var elDto))
                return false;

            int cmp = elDto.CompareTo(refDto);
            return Operator switch
            {
                FilterOperator.Equals             => cmp == 0,
                FilterOperator.NotEquals          => cmp != 0,
                FilterOperator.GreaterThan         => cmp > 0,
                FilterOperator.LessThan            => cmp < 0,
                FilterOperator.GreaterThanOrEqual  => cmp >= 0,
                FilterOperator.LessThanOrEqual     => cmp <= 0,
                _                                  => false
            };
        }

        private bool CompareNull(JsonElement el) => Operator switch
        {
            FilterOperator.Equals    => el.ValueKind == JsonValueKind.Null,
            FilterOperator.NotEquals => el.ValueKind != JsonValueKind.Null,
            _                        => false
        };
    }
}

