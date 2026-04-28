//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Linq.Expressions;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// An <see cref="EventFilter"/> that deserializes the event data payload into a strongly-typed
    /// <typeparamref name="TEvent"/> object and evaluates a compiled
    /// <see cref="Expression{TDelegate}"/> predicate against it.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The CLR type that the JSON payload is deserialized into before the predicate is applied.
    /// The type must be deserializable by <see cref="System.Text.Json.JsonSerializer"/>.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Deserialization relies on the same <see cref="IEventDataDeserializer"/> pipeline that
    /// <see cref="EventDataFilter"/> uses: the <see cref="EventSubscriptionContext"/> is first
    /// consulted for a DI-registered deserializer that handles the event's
    /// <c>datacontenttype</c>; the built-in <see cref="JsonEventDataDeserializer"/> is used as
    /// the fallback.  The resulting <see cref="JsonElement"/> is then deserialized into
    /// <typeparamref name="TEvent"/> via <see cref="JsonSerializer.Deserialize{TValue}(JsonElement, JsonSerializerOptions?)"/>.
    /// </para>
    /// <para>
    /// The <see cref="Expression{TDelegate}"/> is compiled once on first use and cached for
    /// subsequent invocations.  Returns <c>false</c> silently when the event data cannot be
    /// represented as JSON, when deserialization into <typeparamref name="TEvent"/> fails, or
    /// when the predicate itself throws.
    /// </para>
    /// </remarks>
    public sealed class TypedEventDataFilter<TEvent> : EventFilter, ITypedEventDataFilter
    {
        private readonly Expression<Func<TEvent, bool>> _expression;
        private Func<TEvent, bool>? _compiled;
        private readonly JsonSerializerOptions? _serializerOptions;

        /// <summary>
        /// Initialises a new <see cref="TypedEventDataFilter{TEvent}"/> with the supplied
        /// <paramref name="predicate"/> expression.
        /// </summary>
        /// <param name="predicate">
        /// A strongly-typed predicate expression that will be compiled and evaluated against
        /// the deserialized event data.
        /// </param>
        /// <param name="serializerOptions">
        /// Optional <see cref="JsonSerializerOptions"/> used when deserializing the JSON payload
        /// into <typeparamref name="TEvent"/>.  When <c>null</c> the default options are used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="predicate"/> is <c>null</c>.
        /// </exception>
        public TypedEventDataFilter(
            Expression<Func<TEvent, bool>> predicate,
            JsonSerializerOptions? serializerOptions = null)
        {
            _expression = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _serializerOptions = serializerOptions;
        }

        /// <summary>
        /// Gets the original (uncompiled) predicate expression.
        /// </summary>
        public Expression<Func<TEvent, bool>> Predicate => _expression;

        /// <summary>
        /// Gets the <see cref="JsonSerializerOptions"/> used during deserialization, or
        /// <c>null</c> when the defaults are used.
        /// </summary>
        public JsonSerializerOptions? SerializerOptions => _serializerOptions;

        // ── ITypedEventDataFilter ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        Type ITypedEventDataFilter.EventType => typeof(TEvent);

        /// <inheritdoc/>
        LambdaExpression ITypedEventDataFilter.PredicateExpression => _expression;

        // ── Visitor ───────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">
        /// Always thrown: <see cref="TypedEventDataFilter{TEvent}"/> cannot be serialized to JSON
        /// because lambda expressions are not representable as JSON.
        /// </exception>
        public override TResult Accept<TResult>(IEventFilterVisitor<TResult> visitor)
            => visitor.VisitTyped(this);

        // Lazily compile the expression on first use (thread-safe via Interlocked).
        private Func<TEvent, bool> CompiledPredicate
            => _compiled ??= _expression.Compile();

        /// <inheritdoc/>
        public override bool Matches(CloudEvent @event, EventSubscriptionContext context)
        {
            if (@event is null)
                return false;

            var jsonData = context.GetJsonData(@event);
            if (jsonData is null)
                return false;

            TEvent? typed;
            try
            {
                typed = JsonSerializer.Deserialize<TEvent>(jsonData.Value, _serializerOptions);
            }
            catch
            {
                return false;
            }

            if (typed is null)
                return false;

            try
            {
                return CompiledPredicate(typed);
            }
            catch
            {
                return false;
            }
        }
    }
}



