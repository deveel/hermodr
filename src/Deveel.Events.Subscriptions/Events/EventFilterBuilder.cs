//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Provides a fluent API for constructing an <see cref="IEventFilter"/>.
    /// </summary>
    public sealed class EventFilterBuilder
    {
        private readonly List<IEventFilter> _filters = new();

        /// <summary>
        /// Initialises an empty builder. Call the fluent <c>With*</c> methods to add
        /// filter criteria, then <see cref="Build"/> to obtain the composed filter.
        /// </summary>
        public EventFilterBuilder() { }

        // ── Internal helper ─────────────────────────────────────────────────────────

        private void AddFilter(IEventFilter filter) => _filters.Add(filter);

        // ── Envelope-attribute filters ──────────────────────────────────────────────

        /// <summary>
        /// Restricts the subscription to events whose <c>type</c> exactly equals
        /// <paramref name="type"/>.
        /// </summary>
        public EventFilterBuilder WithType(string type)
        {
            AddFilter(EventAttributeFilter.Type(type));
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>type</c> satisfies
        /// <paramref name="pattern"/>.
        /// A trailing <c>*</c> (e.g. <c>"com.example.*"</c>) uses a prefix match;
        /// a leading <c>*</c> (e.g. <c>"*.placed"</c>) uses a suffix match;
        /// anything else uses an exact match.
        /// </summary>
        public EventFilterBuilder WithTypePattern(string pattern)
        {
            AddFilter(EventAttributeFilter.Type(pattern, parseWildcard: true));
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>source</c> URI string
        /// exactly equals <paramref name="source"/>.
        /// </summary>
        public EventFilterBuilder WithSource(string source)
        {
            AddFilter(new EventAttributeFilter("source", source));
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>source</c> URI string
        /// satisfies <paramref name="pattern"/> (same wildcard rules as
        /// <see cref="WithTypePattern"/>).
        /// </summary>
        public EventFilterBuilder WithSourcePattern(string pattern)
        {
            AddFilter(EventAttributeFilter.For("source", pattern, parseWildcard: true));
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>subject</c> attribute
        /// exactly equals <paramref name="subject"/>.
        /// </summary>
        public EventFilterBuilder WithSubject(string subject)
        {
            AddFilter(new EventAttributeFilter("subject", subject));
            return this;
        }

        /// <summary>
        /// Restricts the subscription to events whose <c>subject</c> attribute
        /// satisfies <paramref name="pattern"/> (same wildcard rules as
        /// <see cref="WithTypePattern"/>).
        /// </summary>
        public EventFilterBuilder WithSubjectPattern(string pattern)
        {
            AddFilter(EventAttributeFilter.For("subject", pattern, parseWildcard: true));
            return this;
        }

        /// <summary>
        /// Adds an arbitrary <see cref="IEventFilter"/> to the subscription.
        /// All added filters must pass for the subscription to match.
        /// </summary>
        public EventFilterBuilder With(IEventFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            AddFilter(filter);
            return this;
        }
        

        // ── Field filters (typed value comparison) ──────────────────────────────────

        /// <summary>
        /// Adds an <see cref="EventDataFilter"/> that navigates to <paramref name="path"/>
        /// in the JSON body and requires an exact string match with <paramref name="value"/>.
        /// </summary>
        public EventFilterBuilder WithField(string path, string value)
        {
            AddFilter(EventDataFilter.Create(path, FilterOperator.Equals, value));
            return this;
        }

        /// <summary>Adds an <see cref="EventDataFilter"/> comparing <paramref name="path"/> with a <see cref="string"/> value.</summary>
        public EventFilterBuilder WithField(string path, FilterOperator @operator, string value)
        {
            AddFilter(EventDataFilter.Create(path, @operator, value));
            return this;
        }

        /// <summary>Adds an <see cref="EventDataFilter"/> comparing <paramref name="path"/> with a <see cref="bool"/> value.</summary>
        public EventFilterBuilder WithField(string path, FilterOperator @operator, bool value)
        {
            AddFilter(EventDataFilter.Create(path, @operator, value));
            return this;
        }

        /// <summary>Adds an <see cref="EventDataFilter"/> comparing <paramref name="path"/> with an <see cref="int"/> value.</summary>
        public EventFilterBuilder WithField(string path, FilterOperator @operator, int value)
        {
            AddFilter(EventDataFilter.Create(path, @operator, value));
            return this;
        }

        /// <summary>Adds an <see cref="EventDataFilter"/> comparing <paramref name="path"/> with a <see cref="long"/> value.</summary>
        public EventFilterBuilder WithField(string path, FilterOperator @operator, long value)
        {
            AddFilter(EventDataFilter.Create(path, @operator, value));
            return this;
        }

        /// <summary>Adds an <see cref="EventDataFilter"/> comparing <paramref name="path"/> with a <see cref="double"/> value.</summary>
        public EventFilterBuilder WithField(string path, FilterOperator @operator, double value)
        {
            AddFilter(EventDataFilter.Create(path, @operator, value));
            return this;
        }

        /// <summary>Adds an <see cref="EventDataFilter"/> comparing <paramref name="path"/> with a <see cref="DateTime"/> value.</summary>
        public EventFilterBuilder WithField(string path, FilterOperator @operator, DateTime value)
        {
            AddFilter(EventDataFilter.Create(path, @operator, value));
            return this;
        }

        /// <summary>Adds an <see cref="EventDataFilter"/> comparing <paramref name="path"/> with a <see cref="DateTimeOffset"/> value.</summary>
        public EventFilterBuilder WithField(string path, FilterOperator @operator, DateTimeOffset value)
        {
            AddFilter(EventDataFilter.Create(path, @operator, value));
            return this;
        }


        // ── Build ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds and returns the composed <see cref="IEventFilter"/> (AND of all added
        /// child filters). An empty builder produces a filter that matches every event.
        /// </summary>
        public IEventFilter Build() => LogicalEventFilter.And(_filters.ToArray());
    }
}
