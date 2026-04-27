//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// A fully-serializable model of an event subscription filter, designed to be stored in
    /// any persistence backend (relational DB, document DB, key-value store, etc.) and later
    /// converted back into a runtime <see cref="EventSubscriptionFilter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every property uses only primitive types or other serializable model classes, so the
    /// whole object graph round-trips through <c>System.Text.Json</c> without any custom
    /// converters.  The <see cref="DataExpression"/> property leverages
    /// <see cref="JsonPolymorphicAttribute"/> on the <see cref="FilterExpression"/> hierarchy
    /// (discriminated by a <c>"$kind"</c> property).
    /// </para>
    /// <para>
    /// <strong>Relational mapping guidance</strong> — all scalar envelope columns can be
    /// stored in typed columns; <see cref="Extensions"/> as a JSON column or a sibling
    /// lookup table; <see cref="DataExpression"/> as a <c>JSON</c>/<c>JSONB</c> column.
    /// </para>
    /// <code language="sql">
    /// CREATE TABLE subscription_filters (
    ///     id            UUID PRIMARY KEY,
    ///     type_value    VARCHAR(256),
    ///     type_mode     VARCHAR(16),
    ///     source_value  VARCHAR(512),
    ///     source_mode   VARCHAR(16),
    ///     subject_value VARCHAR(256),
    ///     subject_mode  VARCHAR(16),
    ///     extensions    JSONB,
    ///     data_expr     JSONB
    /// );
    /// </code>
    /// <para>
    /// <strong>Runtime predicates are not representable</strong> — if the source
    /// <see cref="EventSubscriptionFilter"/> carries a <c>Predicate</c> delegate or a
    /// <see cref="TypedDataFilter{T}"/> / <see cref="JsonPredicateDataFilter"/>, those
    /// fields are silently dropped during <see cref="From"/> conversion and a warning is
    /// surfaced via <see cref="HasUnserializablePredicates"/>.
    /// </para>
    /// </remarks>
    public sealed class EventSubscriptionFilterModel
    {
        // ── Envelope attribute filters ──────────────────────────────────────────────

        /// <summary>Filter applied to the CloudEvents <c>type</c> attribute.</summary>
        public AttributeFilterModel? Type { get; set; }

        /// <summary>Filter applied to the CloudEvents <c>source</c> attribute (as a URI string).</summary>
        public AttributeFilterModel? Source { get; set; }

        /// <summary>Filter applied to the CloudEvents <c>subject</c> attribute.</summary>
        public AttributeFilterModel? Subject { get; set; }

        /// <summary>
        /// Filters applied to CloudEvents extension attributes, keyed by attribute name.
        /// </summary>
        public Dictionary<string, AttributeFilterModel>? Extensions { get; set; }

        // ── Body / data filter ──────────────────────────────────────────────────────

        /// <summary>
        /// A fully serializable expression tree applied to the JSON event body.
        /// Replaces the runtime <see cref="JsonPathDataFilter"/> and
        /// <see cref="JsonPredicateDataFilter"/> for persistence scenarios.
        /// </summary>
        /// <remarks>
        /// Supported node types: <see cref="JsonPathComparisonExpression"/>,
        /// <see cref="AndFilterExpression"/>, <see cref="OrFilterExpression"/>,
        /// <see cref="NotFilterExpression"/>.
        /// </remarks>
        public FilterExpression? DataExpression { get; set; }

        // ── Diagnostics ─────────────────────────────────────────────────────────────

        /// <summary>
        /// <c>true</c> when the original <see cref="EventSubscriptionFilter"/> contained a
        /// runtime-only predicate or data filter that could not be captured in this model.
        /// The model still represents the serializable subset of the filter, but is <em>less
        /// restrictive</em> than the original at runtime.
        /// </summary>
        public bool HasUnserializablePredicates { get; set; }

        // ── Serialization options ───────────────────────────────────────────────────

        /// <summary>
        /// Default <see cref="JsonSerializerOptions"/> configured for polymorphic
        /// <see cref="FilterExpression"/> serialization.
        /// </summary>
        public static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // ── Conversion: model → runtime ─────────────────────────────────────────────

        /// <summary>
        /// Builds a runtime <see cref="EventSubscriptionFilter"/> from this model.
        /// </summary>
        /// <remarks>
        /// When <see cref="DataExpression"/> is set it is wrapped in a
        /// <see cref="JsonPredicateDataFilter"/> so it participates in the normal
        /// <see cref="EventDataFilter.Matches"/> pipeline.
        /// </remarks>
        public EventSubscriptionFilter ToRuntimeFilter()
        {
            var filter = new EventSubscriptionFilter
            {
                TypeFilter    = Type?.ToAttributeFilter(),
                SourceFilter  = Source?.ToAttributeFilter(),
                SubjectFilter = Subject?.ToAttributeFilter(),
            };

            if (Extensions is { Count: > 0 })
            {
                filter.ExtensionFilters = Extensions.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToAttributeFilter(),
                    StringComparer.OrdinalIgnoreCase);
            }

            if (DataExpression is not null)
            {
                var expr = DataExpression;          // capture for closure
                filter.DataFilter = EventDataFilter.JsonPredicate(e => expr.Evaluate(e));
            }

            return filter;
        }

        // ── Conversion: runtime → model ─────────────────────────────────────────────

        /// <summary>
        /// Creates an <see cref="EventSubscriptionFilterModel"/> from the runtime
        /// <paramref name="filter"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Envelope attribute filters (<c>type</c>, <c>source</c>, <c>subject</c>,
        /// extensions) are always captured.
        /// </para>
        /// <para>
        /// The <see cref="EventSubscriptionFilter.DataFilter"/> is captured only when it is a
        /// <see cref="JsonPathDataFilter"/> — its path and value filter are converted to a
        /// single <see cref="JsonPathComparisonExpression"/>.  For any other
        /// <see cref="EventDataFilter"/> sub-type (including <see cref="JsonPredicateDataFilter"/>
        /// and <see cref="TypedDataFilter{T}"/>) the field is ignored and
        /// <see cref="HasUnserializablePredicates"/> is set to <c>true</c>.
        /// </para>
        /// <para>
        /// The <see cref="EventSubscriptionFilter.Predicate"/> delegate can never be
        /// captured; it also sets <see cref="HasUnserializablePredicates"/>.
        /// </para>
        /// </remarks>
        public static EventSubscriptionFilterModel From(EventSubscriptionFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);

            var model = new EventSubscriptionFilterModel
            {
                Type    = filter.TypeFilter    is null ? null : AttributeFilterModel.From(filter.TypeFilter),
                Source  = filter.SourceFilter  is null ? null : AttributeFilterModel.From(filter.SourceFilter),
                Subject = filter.SubjectFilter is null ? null : AttributeFilterModel.From(filter.SubjectFilter),
            };

            if (filter.ExtensionFilters is { Count: > 0 })
            {
                model.Extensions = filter.ExtensionFilters.ToDictionary(
                    kv => kv.Key,
                    kv => AttributeFilterModel.From(kv.Value));
            }

            // DataFilter — only JsonPathDataFilter is representable
            if (filter.DataFilter is JsonPathDataFilter jpdf)
            {
                var op = jpdf.ValueFilter.MatchMode switch
                {
                    FilterMatchMode.Prefix => FilterOperator.StartsWith,
                    FilterMatchMode.Suffix => FilterOperator.EndsWith,
                    _                      => FilterOperator.Equals
                };
                model.DataExpression = FilterExpression.JsonPath(jpdf.Path, op, jpdf.ValueFilter.Value);
            }
            else if (filter.DataFilter is not null)
            {
                // JsonPredicateDataFilter / TypedDataFilter<T> — not serializable
                model.HasUnserializablePredicates = true;
            }

            // Runtime Predicate delegate — never serializable
            if (filter.Predicate is not null)
                model.HasUnserializablePredicates = true;

            return model;
        }

        // ── JSON helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Serialises this model to a JSON string using <see cref="DefaultJsonOptions"/>.
        /// </summary>
        public string ToJson(JsonSerializerOptions? options = null)
            => JsonSerializer.Serialize(this, options ?? DefaultJsonOptions);

        /// <summary>
        /// Deserialises an <see cref="EventSubscriptionFilterModel"/> from a JSON string.
        /// </summary>
        public static EventSubscriptionFilterModel? FromJson(string json, JsonSerializerOptions? options = null)
            => JsonSerializer.Deserialize<EventSubscriptionFilterModel>(json, options ?? DefaultJsonOptions);
    }
}

