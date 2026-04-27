//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Base class for filters that inspect the <em>body</em> (data payload) of a
    /// <see cref="CloudEvent"/> rather than its envelope attributes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three concrete sub-types are provided:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <see cref="JsonPathDataFilter"/> — navigates a dotted property path inside a
    ///       JSON body and compares the leaf value with an <see cref="EventAttributeFilter"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="JsonPredicateDataFilter"/> — applies a caller-supplied
    ///       <c>Func&lt;JsonElement, bool&gt;</c> to the root of the JSON body.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <see cref="TypedDataFilter{T}"/> — deserialises the body to the target type
    ///       via an <see cref="EventDataDeserializerProvider"/> (content-type-driven)
    ///       and applies a typed predicate.  Register <see cref="IEventDataDeserializer"/>
    ///       implementations for any wire format (JSON, Protobuf, MessagePack, Avro, …).
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// JSON-specific filters (<see cref="JsonPathDataFilter"/>,
    /// <see cref="JsonPredicateDataFilter"/>) return <c>false</c> silently when:
    /// <list type="bullet">
    ///   <item><description>the event's <c>datacontenttype</c> is absent or does not contain <c>"json"</c>;</description></item>
    ///   <item><description>the event data is <c>null</c>, a raw <c>byte[]</c>, or a <c>Stream</c>;</description></item>
    ///   <item><description>deserialisation/parsing fails.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <see cref="TypedDataFilter{T}"/> delegates to an
    /// <see cref="EventDataDeserializerProvider"/> which dispatches to the registered
    /// <see cref="IEventDataDeserializer"/> matching the event's <c>datacontenttype</c>.
    /// Register custom deserializers for non-JSON formats (Protobuf, MessagePack, Avro, …).
    /// </para>
    /// </remarks>
    public abstract class EventDataFilter
    {
        // ── Backward-compatible JSON helpers ────────────────────────────────────
        // These delegate to JsonEventDataDeserializer so that any user-defined
        // subclasses that call TryGetJsonElement() continue to compile and work.

        /// <summary>
        /// Returns <c>true</c> when the event's <c>datacontenttype</c> is JSON-compatible.
        /// </summary>
        protected static bool IsJsonContent(CloudEvent @event)
            => JsonEventDataDeserializer.IsJsonContent(@event);

        /// <summary>
        /// Attempts to expose the event data as a <see cref="JsonElement"/>.
        /// See <see cref="JsonEventDataDeserializer.TryGetJsonElement"/> for the full
        /// resolution order.
        /// </summary>
        protected static bool TryGetJsonElement(CloudEvent @event, out JsonElement element)
            => JsonEventDataDeserializer.TryGetJsonElement(@event, out element);

        // ── Abstract contract ───────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the filter against the data payload of <paramref name="event"/>.
        /// </summary>
        public abstract bool Matches(CloudEvent @event);

        /// <summary>
        /// Evaluates the filter against the data payload of <paramref name="event"/>,
        /// optionally using <paramref name="services"/> to resolve runtime dependencies
        /// such as a DI-registered <see cref="EventDataDeserializerProvider"/>.
        /// </summary>
        /// <remarks>
        /// The default implementation ignores <paramref name="services"/> and delegates to
        /// <see cref="Matches(CloudEvent)"/>.  Override this in filters that need to
        /// resolve services at evaluation time (e.g. <see cref="TypedDataFilter{T}"/>).
        /// </remarks>
        public virtual bool Matches(CloudEvent @event, IServiceProvider? services)
            => Matches(@event);

        // ── Factory methods ─────────────────────────────────────────────────────

        /// <summary>
        /// Creates a filter that navigates to <paramref name="path"/> (dot-separated property
        /// names) inside the JSON body and requires the leaf value to be exactly
        /// <paramref name="value"/>.
        /// </summary>
        public static EventDataFilter JsonPath(string path, string value)
            => new JsonPathDataFilter(path, EventAttributeFilter.Exact(value));

        /// <summary>
        /// Creates a filter that navigates to <paramref name="path"/> inside the JSON body and
        /// matches the leaf value against <paramref name="valueFilter"/>.
        /// </summary>
        public static EventDataFilter JsonPath(string path, EventAttributeFilter valueFilter)
            => new JsonPathDataFilter(path, valueFilter);

        /// <summary>
        /// Creates a filter that navigates to <paramref name="path"/> inside the JSON body and
        /// matches using a wildcard <paramref name="pattern"/> (same rules as
        /// <see cref="EventAttributeFilter.Parse"/>).
        /// </summary>
        public static EventDataFilter JsonPathPattern(string path, string pattern)
            => new JsonPathDataFilter(path, EventAttributeFilter.Parse(pattern));

        /// <summary>
        /// Creates a filter that applies <paramref name="predicate"/> to the root
        /// <see cref="JsonElement"/> of the event body.
        /// </summary>
        public static EventDataFilter JsonPredicate(Func<JsonElement, bool> predicate)
            => new JsonPredicateDataFilter(predicate);

        /// <summary>
        /// Creates a <see cref="TypedDataFilter{T}"/> that deserialises the event body using
        /// the supplied <paramref name="provider"/> (content-type-driven) and applies
        /// <paramref name="predicate"/> to the result.
        /// </summary>
        /// <typeparam name="T">Target CLR type (must be a reference type).</typeparam>
        /// <param name="predicate">The predicate applied to the deserialised value.</param>
        /// <param name="provider">
        /// The <see cref="EventDataDeserializerProvider"/> that selects the deserializer
        /// based on the event's <c>datacontenttype</c>.
        /// When <c>null</c>, <see cref="EventDataDeserializerProvider.Default"/> is used
        /// (JSON-only).
        /// </param>
        public static EventDataFilter Typed<T>(
            Func<T, bool> predicate,
            EventDataDeserializerProvider? provider = null)
            where T : class
            => new TypedDataFilter<T>(predicate, provider);

        /// <summary>
        /// Creates a <see cref="TypedDataFilter{T}"/> backed by a JSON-only provider
        /// configured with the given <paramref name="serializerOptions"/>.
        /// </summary>
        /// <remarks>
        /// Convenience overload for callers that only need JSON deserialization with custom
        /// <see cref="JsonSerializerOptions"/>.  For multi-format scenarios prefer
        /// <see cref="Typed{T}(Func{T,bool}, EventDataDeserializerProvider)"/>.
        /// </remarks>
        public static EventDataFilter Typed<T>(
            Func<T, bool> predicate,
            JsonSerializerOptions serializerOptions)
            where T : class
        {
            var provider = new EventDataDeserializerProvider()
                .Register(new JsonEventDataDeserializer(serializerOptions));
            return new TypedDataFilter<T>(predicate, provider);
        }
    }
}



