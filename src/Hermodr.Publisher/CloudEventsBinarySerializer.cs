//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Hermodr
{
    /// <summary>
    /// Serializes a <see cref="CloudEvent"/> into the body portion of a
    /// <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md#321-binary-content-mode">CloudEvents binary HTTP content mode</see>
    /// message — i.e. the raw <c>data</c> payload only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This serializer is responsible solely for the request <b>body</b> in
    /// binary content mode. The accompanying <c>ce-*</c> HTTP headers and the
    /// per-event <c>Content-Type</c> (derived from <see cref="CloudEvent.DataContentType"/>)
    /// are emitted by the webhook channel via the
    /// <c>CloudEventHttpHeadersMapper</c> and the per-event content-type override,
    /// since the <see cref="IEventSerializer"/> interface can only describe a
    /// single static content type.
    /// </para>
    /// <para>
    /// The <see cref="ContentType"/> and <see cref="BatchContentType"/> values
    /// are fallback defaults used only when the event carries no
    /// <see cref="CloudEvent.DataContentType"/>; the channel is expected to
    /// substitute the event's <c>datacontenttype</c> when present.
    /// </para>
    /// <para>
    /// Batch serialization is not supported — the CloudEvents HTTP binding
    /// specification defines no binary batch mode — and
    /// <see cref="SerializeBatch"/> throws <see cref="NotSupportedException"/>.
    /// </para>
    /// </remarks>
    public class CloudEventsBinarySerializer : IEventSerializer
    {
        private static readonly JsonEventFormatter Formatter = new();

        /// <summary>A shared singleton instance.</summary>
        public static readonly CloudEventsBinarySerializer Default = new();

        /// <inheritdoc/>
        public string Format => EventMessageFormat.CloudEventsBinary;

        /// <summary>
        /// The fallback content type used when an event does not carry a
        /// <see cref="CloudEvent.DataContentType"/>. The channel overrides this
        /// with the event's <c>datacontenttype</c> when present.
        /// </summary>
        public string ContentType => "application/json; charset=utf-8";

        /// <summary>
        /// Binary content mode is not defined for batches; this value is unused.
        /// </summary>
        public string BatchContentType => "application/json; charset=utf-8";

        /// <inheritdoc/>
        /// <returns>
        /// The raw <c>data</c> bytes of <paramref name="event"/> as produced by
        /// <see cref="JsonEventFormatter.EncodeBinaryModeEventData"/>. Returns an
        /// empty byte array when the event has no <c>data</c>.
        /// </returns>
        public byte[] Serialize(CloudEvent @event)
            => Formatter.EncodeBinaryModeEventData(@event).ToArray();

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">
        /// Always thrown — the CloudEvents HTTP binding specification defines
        /// no binary content mode for batches.
        /// </exception>
        public byte[] SerializeBatch(IReadOnlyList<CloudEvent> events)
            => throw new NotSupportedException(
                "Binary content mode is not defined for batches by the CloudEvents HTTP binding specification.");
    }
}