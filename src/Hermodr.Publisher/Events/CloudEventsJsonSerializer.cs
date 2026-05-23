//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Hermodr
{
    /// <summary>
    /// Serializes <see cref="CloudEvent"/> instances in the CloudEvents
    /// structured JSON format (<c>application/cloudevents+json</c>).
    /// Batch deliveries use the CloudEvents batch JSON format
    /// (<c>application/cloudevents-batch+json</c>).
    /// </summary>
    public class CloudEventsJsonSerializer : IEventSerializer
    {
        private static readonly JsonEventFormatter Formatter = new();

        /// <summary>A shared singleton instance.</summary>
        public static readonly CloudEventsJsonSerializer Default = new();

        /// <inheritdoc/>
        public string Format => EventMessageFormat.CloudEventsJson;

        /// <inheritdoc/>
        public string ContentType => "application/cloudevents+json; charset=utf-8";

        /// <inheritdoc/>
        public string BatchContentType => "application/cloudevents-batch+json; charset=utf-8";

        /// <inheritdoc/>
        public byte[] Serialize(CloudEvent @event)
            => Formatter.EncodeStructuredModeMessage(@event, out _).ToArray();

        /// <inheritdoc/>
        public byte[] SerializeBatch(IReadOnlyList<CloudEvent> events)
            => Formatter.EncodeBatchModeMessage(events, out _).ToArray();
    }
}

