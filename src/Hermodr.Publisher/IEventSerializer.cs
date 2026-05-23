//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// Serializes one or more <see cref="CloudEvent"/> instances into
    /// a byte payload suitable for transmission over a message channel.
    /// </summary>
    /// <remarks>
    /// Implementations are keyed by <see cref="Format"/> so that a channel
    /// can select the correct serializer at runtime based on a configured or
    /// per-message format identifier.  Register custom serializers with the
    /// channel-specific DI extension methods (e.g.
    /// <c>UseWebhookMessageSerializer&lt;T&gt;</c>) or by adding them to the
    /// service collection as <see cref="IEventSerializer"/>.
    /// <br/>
    /// Built-in format identifiers are available in <see cref="EventMessageFormat"/>.
    /// </remarks>
    public interface IEventSerializer
    {
        /// <summary>
        /// A string key that identifies this serialization format (e.g.
        /// <c>"json"</c>, <c>"xml"</c>, <c>"cloudevents+json"</c>).
        /// </summary>
        string Format { get; }

        /// <summary>
        /// The MIME content-type produced when serializing a <b>single</b> event,
        /// e.g. <c>application/json; charset=utf-8</c>.
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// The MIME content-type produced when serializing a <b>batch</b> of events.
        /// May be identical to <see cref="ContentType"/> for formats that wrap
        /// multiple events inside the same envelope (e.g. a JSON array).
        /// </summary>
        string BatchContentType { get; }

        /// <summary>Serializes a single <see cref="CloudEvent"/> into bytes.</summary>
        /// <param name="event">The event to serialize.</param>
        /// <returns>
        /// A byte array containing the serialized representation of <paramref name="event"/>
        /// in the format described by <see cref="ContentType"/>.
        /// </returns>
        byte[] Serialize(CloudEvent @event);

        /// <summary>
        /// Serializes a batch of <see cref="CloudEvent"/> instances into a single byte array.
        /// </summary>
        /// <param name="events">The events to serialize. Must contain at least one element.</param>
        /// <returns>
        /// A byte array containing the serialized representation of all <paramref name="events"/>
        /// in the format described by <see cref="BatchContentType"/>.
        /// </returns>
        byte[] SerializeBatch(IReadOnlyList<CloudEvent> events);
    }
}

