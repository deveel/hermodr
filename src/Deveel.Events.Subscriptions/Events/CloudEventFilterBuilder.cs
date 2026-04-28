//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Filters;

namespace Deveel.Events
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
    /// var filter = CloudEventFilterBuilder.New()
    ///     .ByType("com.example.order.placed")
    ///     .BySource("https://example.com")
    ///     .WithField("status", "active")
    ///     .Build();
    ///
    /// var orFilter = CloudEventFilterBuilder.New()
    ///     .AnyOf(b => b
    ///         .ByType("com.example.order.placed")
    ///         .ByType("com.example.order.updated"))
    ///     .Build();
    /// </code>
    /// </remarks>
    public sealed class CloudEventFilterBuilder
    {
        private readonly List<FilterExpression> _expressions = new();

        // ── Envelope-attribute filters ───────────────────────────────────────────────────

        /// <summary>
        /// Adds a condition that matches events whose <c>type</c> attribute exactly equals
        /// <paramref name="type"/>.
        /// </summary>
        public CloudEventFilterBuilder ByType(string type)
        {
            _expressions.Add(CloudEventFilter.ByType(type));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>type</c> attribute satisfies
        /// <paramref name="pattern"/>.
        /// A trailing <c>*</c> (e.g. <c>"com.example.*"</c>) performs a prefix match;
        /// a leading <c>*</c> (e.g. <c>"*.placed"</c>) performs a suffix match;
        /// otherwise an exact match is used.
        /// </summary>
        public CloudEventFilterBuilder ByTypePattern(string pattern)
        {
            _expressions.Add(CloudEventFilter.ByTypePattern(pattern));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>source</c> attribute exactly equals
        /// <paramref name="source"/>.
        /// </summary>
        public CloudEventFilterBuilder BySource(string source)
        {
            _expressions.Add(CloudEventFilter.BySource(source));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>source</c> attribute satisfies
        /// <paramref name="pattern"/> (same wildcard rules as <see cref="ByTypePattern"/>).
        /// </summary>
        public CloudEventFilterBuilder BySourcePattern(string pattern)
        {
            _expressions.Add(CloudEventFilter.BySourcePattern(pattern));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>subject</c> attribute exactly equals
        /// <paramref name="subject"/>.
        /// </summary>
        public CloudEventFilterBuilder BySubject(string subject)
        {
            _expressions.Add(CloudEventFilter.BySubject(subject));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose <c>subject</c> attribute satisfies
        /// <paramref name="pattern"/> (same wildcard rules as <see cref="ByTypePattern"/>).
        /// </summary>
        public CloudEventFilterBuilder BySubjectPattern(string pattern)
        {
            _expressions.Add(CloudEventFilter.BySubjectPattern(pattern));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events whose extension attribute named
        /// <paramref name="extensionName"/> exactly equals <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder ByExtension(string extensionName, string value)
        {
            _expressions.Add(CloudEventFilter.ByExtension(extensionName, value));
            return this;
        }

        // ── Data-payload field filters ────────────────────────────────────────────────────

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// exactly equals <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder WithField(string path, string value)
        {
            _expressions.Add(CloudEventFilter.ByField(path, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder WithField(string path, FilterExpressionType op, string value)
        {
            _expressions.Add(CloudEventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder WithField(string path, FilterExpressionType op, bool value)
        {
            _expressions.Add(CloudEventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder WithField(string path, FilterExpressionType op, int value)
        {
            _expressions.Add(CloudEventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder WithField(string path, FilterExpressionType op, long value)
        {
            _expressions.Add(CloudEventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder WithField(string path, FilterExpressionType op, double value)
        {
            _expressions.Add(CloudEventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder WithField(string path, FilterExpressionType op, DateTime value)
        {
            _expressions.Add(CloudEventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// satisfies the given <paramref name="op"/> compared to <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder WithField(string path, FilterExpressionType op, DateTimeOffset value)
        {
            _expressions.Add(CloudEventFilter.ByField(path, op, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// starts with <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder FieldStartsWith(string path, string value)
        {
            _expressions.Add(CloudEventFilter.FieldStartsWith(path, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// ends with <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder FieldEndsWith(string path, string value)
        {
            _expressions.Add(CloudEventFilter.FieldEndsWith(path, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// contains <paramref name="value"/>.
        /// </summary>
        public CloudEventFilterBuilder FieldContains(string path, string value)
        {
            _expressions.Add(CloudEventFilter.FieldContains(path, value));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// is present in the payload.
        /// </summary>
        public CloudEventFilterBuilder FieldExists(string path)
        {
            _expressions.Add(CloudEventFilter.FieldExists(path));
            return this;
        }

        /// <summary>
        /// Adds a condition that matches events where the JSON data field at <paramref name="path"/>
        /// is absent from the payload.
        /// </summary>
        public CloudEventFilterBuilder FieldNotExists(string path)
        {
            _expressions.Add(CloudEventFilter.FieldNotExists(path));
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
        public CloudEventFilterBuilder AllOf(Action<CloudEventFilterBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var inner = new CloudEventFilterBuilder();
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
        public CloudEventFilterBuilder AnyOf(Action<CloudEventFilterBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var inner = new CloudEventFilterBuilder();
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
        public CloudEventFilterBuilder Not(Action<CloudEventFilterBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var inner = new CloudEventFilterBuilder();
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


