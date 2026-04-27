//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Maintains a prioritized list of <see cref="IEventDataDeserializer"/> implementations
/// and dispatches deserialization to the first one that declares it can handle the
/// incoming event's <c>datacontenttype</c>.
/// </summary>
/// <remarks>
/// <para>
/// The static <see cref="Default"/> instance is pre-configured with a
/// <see cref="JsonEventDataDeserializer"/> and is used by <see cref="TypedDataFilter{T}"/>
/// when no explicit provider is supplied.
/// </para>
/// <para>
/// Register additional deserializers (e.g. Protobuf, MessagePack) for other content
/// types and pass the provider to the filter:
/// </para>
/// <code>
/// var provider = new EventDataDeserializerProvider()
///     .Register(new JsonEventDataDeserializer())
///     .Register(new ProtobufEventDataDeserializer());
///
/// filter.WithData&lt;OrderEvent&gt;(o => o.Amount > 100, provider);
/// </code>
/// </remarks>
public sealed class EventDataDeserializerProvider
{
    private readonly List<IEventDataDeserializer> _deserializers = [];

    // ── Static default ──────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the shared default provider, pre-configured with
    /// <see cref="JsonEventDataDeserializer"/> (no custom options).
    /// </summary>
    public static EventDataDeserializerProvider Default { get; } = CreateDefault();

    private static EventDataDeserializerProvider CreateDefault()
        => new EventDataDeserializerProvider().Register(new JsonEventDataDeserializer());

    // ── Registration ────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a deserializer at the end of the dispatch chain.
    /// </summary>
    /// <remarks>
    /// Deserializers are evaluated in registration order; the first one whose
    /// <see cref="IEventDataDeserializer.CanDeserialize"/> returns <c>true</c> for the
    /// event's <c>datacontenttype</c> is invoked.
    /// </remarks>
    /// <param name="deserializer">The deserializer to add.</param>
    /// <returns>The provider instance (fluent).</returns>
    public EventDataDeserializerProvider Register(IEventDataDeserializer deserializer)
    {
        ArgumentNullException.ThrowIfNull(deserializer);
        _deserializers.Add(deserializer);
        return this;
    }

    // ── Dispatch ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to deserialize the data payload of <paramref name="event"/> to an
    /// instance of <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Fast-path:</b> when <c>event.Data</c> is already an instance of
    /// <typeparamref name="T"/> the registered deserializers are bypassed entirely.
    /// </para>
    /// <para>
    /// Otherwise, the provider iterates its deserializers until one whose
    /// <see cref="IEventDataDeserializer.CanDeserialize"/> returns <c>true</c> for
    /// the event's <c>datacontenttype</c> successfully deserializes the payload.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Target CLR type.</typeparam>
    /// <param name="event">The cloud event to deserialize.</param>
    /// <param name="result">
    /// When the method returns <c>true</c>, the deserialized object; otherwise <c>null</c>.
    /// </param>
    public bool TryDeserialize<T>(CloudEvent @event, out T? result) where T : class
    {
        // Fast path: already the desired type — no deserialization needed.
        if (@event.Data is T direct)
        {
            result = direct;
            return true;
        }

        var contentType = @event.DataContentType;

        foreach (var deserializer in _deserializers)
        {
            if (deserializer.CanDeserialize(contentType) &&
                deserializer.TryDeserialize(@event, out result))
            {
                return true;
            }
        }

        result = null;
        return false;
    }
}

