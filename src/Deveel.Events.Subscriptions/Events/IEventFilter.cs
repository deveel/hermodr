//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Linq.Expressions;
using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Abstract base class for a filter that determines whether a <see cref="CloudEvent"/>
    /// satisfies a specific condition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built-in implementations include:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <see cref="EventAttributeFilter"/> — matches a named CloudEvents envelope attribute
    ///       (including extension attributes via the <c>extension.</c> prefix).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="EventDataFilter"/> — navigates a dot-separated JSON path within the event
    ///       data payload and applies a <see cref="FilterOperator"/> comparison against a typed value.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="LogicalEventFilter"/> — combines multiple <see cref="EventFilter"/> instances
    ///       with AND or OR logic.
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// Use the static factory methods on this class (e.g. <see cref="Type(string)"/>,
    /// <see cref="And(EventFilter[])"/>, <see cref="Create(string,FilterOperator,string)"/>)
    /// to construct filters without referencing the concrete subclasses directly.
    /// </para>
    /// </remarks>
    public abstract class EventFilter
    {
        /// <summary>
        /// Returns <c>true</c> when <paramref name="event"/> satisfies the filter condition.
        /// </summary>
        /// <param name="event">The incoming <see cref="CloudEvent"/> to test.</param>
        /// <param name="context">
        /// The <see cref="EventSubscriptionContext"/> providing runtime services (e.g. a DI
        /// <see cref="IServiceProvider"/> for resolving deserializers).
        /// Pass <see cref="EventSubscriptionContext.Empty"/> when no context is available.
        /// </param>
        public abstract bool Matches(CloudEvent @event, EventSubscriptionContext context);

        // ── Visitor ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Accepts a visitor, dispatching to the appropriate <c>Visit*</c> method on
        /// <paramref name="visitor"/> for the concrete filter type.
        /// </summary>
        /// <typeparam name="TResult">The value type returned by the visitor.</typeparam>
        /// <param name="visitor">The visitor to dispatch to.</param>
        public abstract TResult Accept<TResult>(IEventFilterVisitor<TResult> visitor);

        // ── JSON serialization ────────────────────────────────────────────────────────────

        /// <summary>
        /// Serializes this filter to a JSON string.
        /// </summary>
        /// <returns>A JSON representation of the filter.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when the filter (or any of its children) is a <see cref="TypedEventDataFilter{TEvent}"/>,
        /// which cannot be reliably serialized because lambda expressions are not representable as JSON.
        /// </exception>
        public string ToJson()
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false });
            Accept(new EventFilterJsonWriter(writer));
            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        /// <summary>
        /// Deserializes an <see cref="EventFilter"/> from the supplied <paramref name="json"/> string
        /// that was previously produced by <see cref="ToJson"/>.
        /// </summary>
        /// <param name="json">The JSON string to parse.</param>
        /// <returns>The reconstructed <see cref="EventFilter"/>.</returns>
        /// <exception cref="JsonException">
        /// Thrown when <paramref name="json"/> does not represent a valid serialized filter.
        /// </exception>
        public static EventFilter FromJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            using var doc = JsonDocument.Parse(json);
            return EventFilterJsonReader.Read(doc.RootElement);
        }

        // ── Attribute-based factory methods ──────────────────────────────────────────────

        /// <summary>
        /// Creates a filter that matches the CloudEvents attribute named
        /// <paramref name="attributeName"/> using the supplied <paramref name="value"/>
        /// and <paramref name="matchMode"/>.
        /// </summary>
        /// <param name="attributeName">
        /// A standard attribute name (<c>type</c>, <c>source</c>, <c>subject</c>, <c>id</c>,
        /// <c>time</c>, <c>datacontenttype</c>, <c>dataschema</c>) or an extension attribute
        /// prefixed with <c>extension.</c> (e.g. <c>"extension.tenantid"</c>).
        /// </param>
        /// <param name="value">The value to match against.</param>
        /// <param name="matchMode">The matching strategy. Defaults to <see cref="FilterMatchMode.Exact"/>.</param>
        public static EventAttributeFilter For(
            string attributeName,
            string value,
            FilterMatchMode matchMode = FilterMatchMode.Exact)
            => new(attributeName, value, matchMode);

        /// <summary>
        /// Parses a wildcard <paramref name="pattern"/> and creates a filter that targets
        /// <paramref name="attributeName"/>.
        /// A trailing <c>*</c> produces <see cref="FilterMatchMode.Prefix"/>;
        /// a leading <c>*</c> produces <see cref="FilterMatchMode.Suffix"/>;
        /// anything else produces <see cref="FilterMatchMode.Exact"/>.
        /// </summary>
        public static EventAttributeFilter For(string attributeName, string pattern, bool parseWildcard)
        {
            if (!parseWildcard)
                return new(attributeName, pattern);

            if (pattern.EndsWith('*'))
                return new(attributeName, pattern.TrimEnd('*'), FilterMatchMode.Prefix);
            if (pattern.StartsWith('*'))
                return new(attributeName, pattern.TrimStart('*'), FilterMatchMode.Suffix);
            return new(attributeName, pattern);
        }

        /// <summary>
        /// Creates a filter that matches the CloudEvents <c>type</c> attribute with an exact,
        /// case-sensitive comparison against <paramref name="value"/>.
        /// </summary>
        public static EventAttributeFilter Type(string value)
            => new("type", value, FilterMatchMode.Exact);

        /// <summary>
        /// Creates a filter that matches the CloudEvents <c>type</c> attribute using the
        /// specified <paramref name="matchMode"/>.
        /// </summary>
        public static EventAttributeFilter Type(string value, FilterMatchMode matchMode)
            => new("type", value, matchMode);

        /// <summary>
        /// Parses a wildcard pattern and creates a filter targeting the CloudEvents <c>type</c>
        /// attribute.  When <paramref name="parseWildcard"/> is <c>true</c>, a trailing <c>*</c>
        /// produces <see cref="FilterMatchMode.Prefix"/>, a leading <c>*</c> produces
        /// <see cref="FilterMatchMode.Suffix"/>, and no wildcard produces
        /// <see cref="FilterMatchMode.Exact"/>.
        /// </summary>
        public static EventAttributeFilter Type(string pattern, bool parseWildcard)
            => For("type", pattern, parseWildcard);

        /// <summary>
        /// Creates a filter that tests the CloudEvents extension attribute
        /// <paramref name="extensionName"/> (the <c>extension.</c> prefix is added automatically).
        /// </summary>
        public static EventAttributeFilter ForExtension(
            string extensionName,
            string value,
            FilterMatchMode matchMode = FilterMatchMode.Exact)
            => new("extension." + extensionName, value, matchMode);

        // ── Data-field factory methods ────────────────────────────────────────────────────

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

        // ── Typed-predicate factory methods ──────────────────────────────────────────────

        /// <summary>
        /// Creates a filter that deserializes the event data payload into
        /// <typeparamref name="TEvent"/> and evaluates <paramref name="predicate"/> against it.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The CLR type the JSON payload is deserialized into.
        /// </typeparam>
        /// <param name="predicate">
        /// A strongly-typed predicate expression evaluated against the deserialized object.
        /// </param>
        /// <param name="serializerOptions">
        /// Optional <see cref="JsonSerializerOptions"/> used during deserialization.
        /// </param>
        public static TypedEventDataFilter<TEvent> For<TEvent>(
            Expression<Func<TEvent, bool>> predicate,
            JsonSerializerOptions? serializerOptions = null)
            => new(predicate, serializerOptions);

        // ── Logical factory methods ───────────────────────────────────────────────────────

        /// <summary>
        /// Creates a logical AND filter that passes only when <em>all</em>
        /// <paramref name="filters"/> match the event.
        /// An empty list evaluates to <c>true</c>.
        /// </summary>
        public static LogicalEventFilter And(params EventFilter[] filters)
            => new(LogicalFilterOperator.And, filters ?? throw new ArgumentNullException(nameof(filters)));

        /// <summary>
        /// Creates a logical AND filter that passes only when <em>all</em>
        /// <paramref name="filters"/> match the event.
        /// An empty list evaluates to <c>true</c>.
        /// </summary>
        public static LogicalEventFilter And(IEnumerable<EventFilter> filters)
            => new(LogicalFilterOperator.And, [.. filters ?? throw new ArgumentNullException(nameof(filters))]);

        /// <summary>
        /// Creates a logical OR filter that passes when <em>at least one</em> of
        /// <paramref name="filters"/> matches the event.
        /// An empty list evaluates to <c>false</c>.
        /// </summary>
        public static LogicalEventFilter Or(params EventFilter[] filters)
            => new(LogicalFilterOperator.Or, filters ?? throw new ArgumentNullException(nameof(filters)));

        /// <summary>
        /// Creates a logical OR filter that passes when <em>at least one</em> of
        /// <paramref name="filters"/> matches the event.
        /// An empty list evaluates to <c>false</c>.
        /// </summary>
        public static LogicalEventFilter Or(IEnumerable<EventFilter> filters)
            => new(LogicalFilterOperator.Or, [.. filters ?? throw new ArgumentNullException(nameof(filters))]);
    }
}


