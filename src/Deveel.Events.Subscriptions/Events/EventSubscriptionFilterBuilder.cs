//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Provides a fluent API for constructing an <see cref="EventSubscriptionFilter"/>.
    /// </summary>
    public sealed class EventSubscriptionFilterBuilder
    {
        private readonly EventSubscriptionFilter _filter = new();

        internal EventSubscriptionFilterBuilder() { }

        /// <summary>
        /// Restricts the subscription to events whose <c>type</c> exactly equals
        /// <paramref name="type"/>.
        /// </summary>
        public EventSubscriptionFilterBuilder WithType(string type)
        {
            _filter.TypeFilter = EventAttributeFilter.Exact(type);
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>type</c> satisfies
        /// <paramref name="pattern"/>.
        /// A trailing <c>*</c> (e.g. <c>"com.example.*"</c>) uses a prefix match;
        /// a leading <c>*</c> (e.g. <c>"*.placed"</c>) uses a suffix match;
        /// anything else uses an exact match.
        /// </summary>
        public EventSubscriptionFilterBuilder WithTypePattern(string pattern)
        {
            _filter.TypeFilter = EventAttributeFilter.Parse(pattern);
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>source</c> URI string
        /// exactly equals <paramref name="source"/>.
        /// </summary>
        public EventSubscriptionFilterBuilder WithSource(string source)
        {
            _filter.SourceFilter = EventAttributeFilter.Exact(source);
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>source</c> URI string
        /// satisfies <paramref name="pattern"/> (same wildcard rules as
        /// <see cref="WithTypePattern"/>).
        /// </summary>
        public EventSubscriptionFilterBuilder WithSourcePattern(string pattern)
        {
            _filter.SourceFilter = EventAttributeFilter.Parse(pattern);
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>subject</c> attribute
        /// exactly equals <paramref name="subject"/>.
        /// </summary>
        public EventSubscriptionFilterBuilder WithSubject(string subject)
        {
            _filter.SubjectFilter = EventAttributeFilter.Exact(subject);
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>subject</c> attribute
        /// satisfies <paramref name="pattern"/> (same wildcard rules as
        /// <see cref="WithTypePattern"/>).
        /// </summary>
        public EventSubscriptionFilterBuilder WithSubjectPattern(string pattern)
        {
            _filter.SubjectFilter = EventAttributeFilter.Parse(pattern);
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events that carry the CloudEvents extension attribute
        /// <paramref name="name"/> with a value that exactly equals <paramref name="value"/>.
        /// </summary>
        public EventSubscriptionFilterBuilder WithExtension(string name, string value)
        {
            _filter.ExtensionFilters ??= new Dictionary<string, EventAttributeFilter>(StringComparer.OrdinalIgnoreCase);
            _filter.ExtensionFilters[name] = EventAttributeFilter.Exact(value);
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events that carry the CloudEvents extension attribute
        /// <paramref name="name"/> with a value that satisfies <paramref name="pattern"/>
        /// (same wildcard rules as <see cref="WithTypePattern"/>).
        /// </summary>
        public EventSubscriptionFilterBuilder WithExtensionPattern(string name, string pattern)
        {
            _filter.ExtensionFilters ??= new Dictionary<string, EventAttributeFilter>(StringComparer.OrdinalIgnoreCase);
            _filter.ExtensionFilters[name] = EventAttributeFilter.Parse(pattern);
            return this;
        }

        /// <summary>
        /// Applies the given <paramref name="predicate"/> as an additional guard that is
        /// evaluated after all attribute and data filters pass.
        /// </summary>
        public EventSubscriptionFilterBuilder WithPredicate(Func<CloudEvent, bool> predicate)
        {
            _filter.Predicate = predicate;
            return this;
        }

        // ── Body / data filters ─────────────────────────────────────────────────────

        /// <summary>
        /// Applies a <see cref="JsonPathDataFilter"/> that navigates <paramref name="path"/>
        /// (dot-separated property names) in the JSON body and requires an exact match with
        /// <paramref name="value"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// fb.WithJsonPath("order.customer.tier", "gold")
        /// </code>
        /// </example>
        public EventSubscriptionFilterBuilder WithJsonPath(string path, string value)
        {
            _filter.DataFilter = EventDataFilter.JsonPath(path, value);
            return this;
        }

        /// <summary>
        /// Applies a <see cref="JsonPathDataFilter"/> that navigates <paramref name="path"/>
        /// in the JSON body and matches the leaf value against <paramref name="valueFilter"/>.
        /// </summary>
        public EventSubscriptionFilterBuilder WithJsonPath(string path, EventAttributeFilter valueFilter)
        {
            _filter.DataFilter = EventDataFilter.JsonPath(path, valueFilter);
            return this;
        }

        /// <summary>
        /// Applies a <see cref="JsonPathDataFilter"/> that navigates <paramref name="path"/>
        /// in the JSON body and matches using a wildcard <paramref name="pattern"/>
        /// (trailing <c>*</c> → prefix, leading <c>*</c> → suffix, no wildcard → exact).
        /// </summary>
        public EventSubscriptionFilterBuilder WithJsonPathPattern(string path, string pattern)
        {
            _filter.DataFilter = EventDataFilter.JsonPathPattern(path, pattern);
            return this;
        }

        /// <summary>
        /// Applies a <see cref="JsonPredicateDataFilter"/> that evaluates
        /// <paramref name="predicate"/> against the root <see cref="JsonElement"/>
        /// of the JSON body.
        /// </summary>
        public EventSubscriptionFilterBuilder WithJsonPredicate(Func<JsonElement, bool> predicate)
        {
            _filter.DataFilter = EventDataFilter.JsonPredicate(predicate);
            return this;
        }

        /// <summary>
        /// Applies a <see cref="TypedDataFilter{T}"/> that deserialises the event body using
        /// the supplied <paramref name="provider"/> (content-type-driven) and evaluates
        /// <paramref name="predicate"/> against the result.
        /// </summary>
        /// <typeparam name="T">Target CLR type.</typeparam>
        /// <param name="predicate">Filter predicate applied to the typed data.</param>
        /// <param name="provider">
        /// The <see cref="EventDataDeserializerProvider"/> that selects the deserializer
        /// based on the event's <c>datacontenttype</c>.
        /// When <c>null</c>, <see cref="EventDataDeserializerProvider.Default"/> is used
        /// (JSON-only out of the box).
        /// </param>
        /// <example>
        /// Using a custom Protobuf deserializer registered alongside JSON:
        /// <code>
        /// var provider = new EventDataDeserializerProvider()
        ///     .Register(new JsonEventDataDeserializer())
        ///     .Register(new ProtobufEventDataDeserializer());
        ///
        /// fb.WithData&lt;OrderEvent&gt;(o => o.Amount > 100, provider);
        /// </code>
        /// </example>
        public EventSubscriptionFilterBuilder WithData<T>(
            Func<T, bool> predicate,
            EventDataDeserializerProvider? provider = null)
            where T : class
        {
            _filter.DataFilter = EventDataFilter.Typed(predicate, provider);
            return this;
        }

        /// <summary>
        /// Applies a <see cref="TypedDataFilter{T}"/> backed by a JSON-only provider
        /// configured with the given <paramref name="serializerOptions"/>.
        /// </summary>
        /// <typeparam name="T">Target CLR type.</typeparam>
        /// <param name="predicate">Filter predicate applied to the typed data.</param>
        /// <param name="serializerOptions">
        /// <see cref="JsonSerializerOptions"/> used when deserialising from JSON.
        /// </param>
        /// <remarks>
        /// For multi-format scenarios (JSON + Protobuf, etc.) prefer
        /// <see cref="WithData{T}(Func{T,bool}, EventDataDeserializerProvider)"/> with a
        /// fully configured <see cref="EventDataDeserializerProvider"/>.
        /// </remarks>
        public EventSubscriptionFilterBuilder WithData<T>(
            Func<T, bool> predicate,
            JsonSerializerOptions serializerOptions)
            where T : class
        {
            _filter.DataFilter = EventDataFilter.Typed(predicate, serializerOptions);
            return this;
        }

        /// <summary>
        /// Applies a serializable <see cref="FilterExpression"/> tree as the data filter.
        /// Use this overload when you need the filter to be persistable to a database.
        /// </summary>
        /// <remarks>
        /// The expression is wrapped in a <see cref="JsonPredicateDataFilter"/> at runtime.
        /// Construct the tree using the factory methods on <see cref="FilterExpression"/>:
        /// <code>
        /// fb.WithDataExpression(
        ///     FilterExpression.And(
        ///         FilterExpression.JsonPath("tier", "gold"),
        ///         FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "100")))
        /// </code>
        /// </remarks>
        public EventSubscriptionFilterBuilder WithDataExpression(FilterExpression expression)
        {
            var expr = expression ?? throw new ArgumentNullException(nameof(expression));
            _filter.DataFilter = EventDataFilter.JsonPredicate(e => expr.Evaluate(e));
            return this;
        }

        /// <summary>
        /// Builds and returns the configured <see cref="EventSubscriptionFilter"/>.
        /// </summary>
        public EventSubscriptionFilter Build() => _filter;
    }
}




