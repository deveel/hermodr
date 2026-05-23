//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Filters;

namespace Hermodr
{
    /// <summary>
    /// A fluent builder for composing <see cref="FilterExpression"/> instances that target
    /// CloudEvent envelope attributes and JSON data-payload fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All conditions added at the top level are combined with a logical AND by default.
    /// Use <see cref="AnyOf"/> to create an OR group, <see cref="AllOf"/> to create an explicit
    /// AND sub-group, and <see cref="Not"/> to negate a group.
    /// </para>
    /// <para>Example usage:</para>
    /// <code language="csharp">
    /// var filter = EventFilterBuilder.New()
    ///     .ByType("com.example.order.placed")
    ///     .BySource("https://example.com")
    ///     .WithField("status", "active")
    ///     .Build();
    ///
    /// var orFilter = EventFilterBuilder.New()
    ///     .AnyOf(b => b
    ///         .ByType("com.example.order.placed")
    ///         .ByType("com.example.order.updated"))
    ///     .Build();
    /// </code>
    /// </remarks>
    public sealed class EventFilterBuilder
    {
        private readonly List<FilterExpression> _expressions = new();

        // ── Envelope-attribute filters ───────────────────────────────────────────────────

        /// <summary>
        /// Adds a condition that matches events whose <c>type</c> attribute exactly equals
        /// <paramref name="type"/>.
        /// </summary>
        public EventFilterBuilder ByType(string type)
        {
            _expressions.Add(EventFilter.ByType(type));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>type</c> attribute satisfies
        /// <paramref name="pattern"/>.
        /// A trailing <c>*</c> (e.g. <c>"com.example.*"</c>) performs a prefix match;
        /// a leading <c>*</c> (e.g. <c>"*.placed"</c>) performs a suffix match;
        /// otherwise an exact match is used.
        /// </summary>
        public EventFilterBuilder ByTypePattern(string pattern)
        {
            _expressions.Add(EventFilter.ByTypePattern(pattern));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>source</c> attribute exactly equals
        /// <paramref name="source"/>.
        /// </summary>
        public EventFilterBuilder BySource(string source)
        {
            _expressions.Add(EventFilter.BySource(source));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>source</c> attribute satisfies
        /// <paramref name="pattern"/> (same wildcard rules as <see cref="ByTypePattern"/>).
        /// </summary>
        public EventFilterBuilder BySourcePattern(string pattern)
        {
            _expressions.Add(EventFilter.BySourcePattern(pattern));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>subject</c> attribute exactly equals
        /// <paramref name="subject"/>.
        /// </summary>
        public EventFilterBuilder BySubject(string subject)
        {
            _expressions.Add(EventFilter.BySubject(subject));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>subject</c> attribute satisfies
        /// <paramref name="pattern"/> (same wildcard rules as <see cref="ByTypePattern"/>).
        /// </summary>
        public EventFilterBuilder BySubjectPattern(string pattern)
        {
            _expressions.Add(EventFilter.BySubjectPattern(pattern));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose extension attribute named
        /// <paramref name="extensionName"/> exactly equals <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder ByExtension(string extensionName, string value)
        {
            _expressions.Add(EventFilter.ByExtension(extensionName, value));
            return this;
        }

        // ── Data-payload field filters ────────────────────────────────────────────────────

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// exactly equals <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder WithField(string path, string value)
        {
            _expressions.Add(EventFilter.ByField(path, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder WithField(string path, FilterExpressionType op, string value)
        {
            _expressions.Add(EventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder WithField(string path, FilterExpressionType op, bool value)
        {
            _expressions.Add(EventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder WithField(string path, FilterExpressionType op, int value)
        {
            _expressions.Add(EventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder WithField(string path, FilterExpressionType op, long value)
        {
            _expressions.Add(EventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder WithField(string path, FilterExpressionType op, double value)
        {
            _expressions.Add(EventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder WithField(string path, FilterExpressionType op, DateTime value)
        {
            _expressions.Add(EventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder WithField(string path, FilterExpressionType op, DateTimeOffset value)
        {
            _expressions.Add(EventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// starts with <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder FieldStartsWith(string path, string value)
        {
            _expressions.Add(EventFilter.FieldStartsWith(path, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// ends with <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder FieldEndsWith(string path, string value)
        {
            _expressions.Add(EventFilter.FieldEndsWith(path, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// contains <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder FieldContains(string path, string value)
        {
            _expressions.Add(EventFilter.FieldContains(path, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// is present in the payload.
        /// </summary>
        public EventFilterBuilder FieldExists(string path)
        {
            _expressions.Add(EventFilter.FieldExists(path));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// is absent from the payload.
        /// </summary>
        public EventFilterBuilder FieldNotExists(string path)
        {
            _expressions.Add(EventFilter.FieldNotExists(path));
            return this;
        }

        // ── Logical grouping ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a condition group where all conditions configured in <paramref name="configure"/>
        /// must be satisfied (logical AND). Useful for explicit grouping inside an
        /// <see cref="AnyOf"/> scope.
        /// </summary>
        /// <param name="configure">A delegate that configures the inner builder.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configure"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the inner builder has no conditions.
        /// </exception>
        public EventFilterBuilder AllOf(Action<EventFilterBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var inner = new EventFilterBuilder();
            configure(inner);
            _expressions.Add(inner.BuildRequired(nameof(AllOf)));
            return this;
        }

        /// <summary>
        /// Adds a condition group where at least one condition configured in
        /// <paramref name="configure"/> must be satisfied (logical OR).
        /// </summary>
        /// <param name="configure">A delegate that configures the inner builder.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configure"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the inner builder has no conditions.
        /// </exception>
        public EventFilterBuilder AnyOf(Action<EventFilterBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var inner = new EventFilterBuilder();
            configure(inner);

            if (inner._expressions.Count == 0)
                throw new InvalidOperationException(
                    "AnyOf requires at least one condition in the inner builder.");

            var orChain = inner._expressions.Aggregate(FilterExpression.Or);
            _expressions.Add(orChain);
            return this;
        }

        /// <summary>
        /// Adds the logical negation of all conditions configured in <paramref name="configure"/>.
        /// All inner conditions are ANDed together and then negated.
        /// </summary>
        /// <param name="configure">A delegate that configures the inner builder.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configure"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the inner builder has no conditions.
        /// </exception>
        public EventFilterBuilder Not(Action<EventFilterBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var inner = new EventFilterBuilder();
            configure(inner);
            _expressions.Add(FilterExpression.Not(inner.BuildRequired(nameof(Not))));
            return this;
        }

        // ── Build ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a <see cref="FilterExpression"/> by combining all accumulated conditions
        /// with a logical AND.
        /// </summary>
        /// <returns>
        /// A <see cref="FilterExpression"/> representing all conditions. Returns
        /// <see cref="FilterExpression.Empty"/> when no conditions have been added.
        /// </returns>
        public FilterExpression Build()
            => _expressions.Count == 0
                ? FilterExpression.Empty
                : _expressions.Aggregate(FilterExpression.And);

        // ── Private helpers ──────────────────────────────────────────────────────────────

        private FilterExpression BuildRequired(string callerName)
        {
            if (_expressions.Count == 0)
                throw new InvalidOperationException(
                    $"{callerName} requires at least one condition in the inner builder.");

            return _expressions.Aggregate(FilterExpression.And);
        }
    }
}


