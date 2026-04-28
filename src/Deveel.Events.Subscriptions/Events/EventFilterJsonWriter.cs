//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// An <see cref="IEventFilterVisitor{TResult}"/> that serializes an
    /// <see cref="EventFilter"/> tree into a <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <remarks>
    /// JSON schema used by each filter kind:
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="EventAttributeFilter"/> →
    ///     <c>{"$filter":"attribute","attribute":"&lt;name&gt;","value":"&lt;v&gt;","matchMode":"&lt;Exact|Prefix|Suffix&gt;"}</c>
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="EventDataFilter"/> →
    ///     <c>{"$filter":"data","path":"&lt;dot.path&gt;","operator":"&lt;Op&gt;","value":&lt;v&gt;,"valueType":"&lt;type&gt;"}</c>
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="LogicalEventFilter"/> (AND) → <c>{"$filter":"and","filters":[…]}</c>
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="LogicalEventFilter"/> (OR) → <c>{"$filter":"or","filters":[…]}</c>
    ///   </description></item>
    /// </list>
    /// </remarks>
    internal sealed class EventFilterJsonWriter : IEventFilterVisitor<bool>
    {
        private readonly Utf8JsonWriter _writer;

        internal EventFilterJsonWriter(Utf8JsonWriter writer)
        {
            _writer = writer;
        }

        // ── IEventFilterVisitor<bool> ─────────────────────────────────────────────────

        public bool VisitAttribute(EventAttributeFilter filter)
        {
            _writer.WriteStartObject();
            _writer.WriteString("$filter", "attribute");
            _writer.WriteString("attribute", filter.AttributeName);
            _writer.WriteString("value", filter.Value);
            _writer.WriteString("matchMode", filter.MatchMode.ToString());
            _writer.WriteEndObject();
            return true;
        }

        public bool VisitData(EventDataFilter filter)
        {
            _writer.WriteStartObject();
            _writer.WriteString("$filter", "data");
            _writer.WriteString("path", filter.Path);
            _writer.WriteString("operator", filter.Operator.ToString());
            WriteTypedValue(filter.Value);
            _writer.WriteEndObject();
            return true;
        }

        public bool VisitLogical(LogicalEventFilter filter)
        {
            _writer.WriteStartObject();
            _writer.WriteString("$filter", filter.Kind == LogicalFilterOperator.And ? "and" : "or");
            _writer.WriteStartArray("filters");
            foreach (var child in filter.Filters)
                child.Accept(this);
            _writer.WriteEndArray();
            _writer.WriteEndObject();
            return true;
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">
        /// Always thrown: <see cref="TypedEventDataFilter{TEvent}"/> cannot be serialized
        /// because lambda expressions are not representable as JSON.
        /// </exception>
        public bool VisitTyped(ITypedEventDataFilter filter)
            => throw new NotSupportedException(
                $"TypedEventDataFilter<{filter.EventType.Name}> cannot be serialized to JSON. " +
                "Lambda expressions are not representable as JSON.");

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private void WriteTypedValue(object? value)
        {
            switch (value)
            {
                case null:
                    _writer.WriteNull("value");
                    _writer.WriteString("valueType", "null");
                    break;
                case bool b:
                    _writer.WriteBoolean("value", b);
                    _writer.WriteString("valueType", "bool");
                    break;
                case int i:
                    _writer.WriteNumber("value", i);
                    _writer.WriteString("valueType", "int");
                    break;
                case long l:
                    _writer.WriteNumber("value", l);
                    _writer.WriteString("valueType", "long");
                    break;
                case double d:
                    _writer.WriteNumber("value", d);
                    _writer.WriteString("valueType", "double");
                    break;
                case string s:
                    _writer.WriteString("value", s);
                    _writer.WriteString("valueType", "string");
                    break;
                case DateTime dt:
                    _writer.WriteString("value", dt.ToString("O"));
                    _writer.WriteString("valueType", "datetime");
                    break;
                case DateTimeOffset dto:
                    _writer.WriteString("value", dto.ToString("O"));
                    _writer.WriteString("valueType", "datetimeoffset");
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported value type '{value.GetType().Name}' in {nameof(EventDataFilter)}.");
            }
        }
    }
}





