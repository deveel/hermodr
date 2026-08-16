//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Well-known transport identifiers used by <see cref="IEventPublishChannelMetadata.Transport"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These constants are provided for interoperability between the framework's own
    /// channel adapters and third-party consumers of the metadata (such as the
    /// AsyncAPI / OpenAPI exporters). They are <strong>not</strong> an enumeration:
    /// any string is a valid transport identifier, and third-party channels are free
    /// to publish their own values (e.g. <c>"kafka"</c>, <c>"nats"</c>).
    /// </para>
    /// <para>
    /// The special value <see cref="Other"/> marks channels that are not message
    /// transports (for example storage or deferral layers such as the Audit Trail
    /// and the Outbox). Exporters skip channels whose transport equals
    /// <see cref="Other"/>.
    /// </para>
    /// </remarks>
    public static class EventTransports
    {
        /// <summary>RabbitMQ broker transport identifier (<c>"rabbitmq"</c>).</summary>
        public const string RabbitMq = "rabbitmq";

        /// <summary>Azure Service Bus transport identifier (<c>"azureservicebus"</c>).</summary>
        public const string AzureServiceBus = "azureservicebus";

        /// <summary>Webhook (signed HTTP fan-out) transport identifier (<c>"webhook"</c>).</summary>
        public const string Webhook = "webhook";

        /// <summary>
        /// Lightweight direct HTTP delivery transport identifier (<c>"http"</c>).
        /// Reserved for the <c>Hermodr.Publisher.Http</c> channel planned for v1.5.0
        /// (roadmap item 13); no channel produces this value in v1.4.0.
        /// </summary>
        public const string Http = "http";

        /// <summary>Dapr pub/sub building block transport identifier (<c>"dapr"</c>).</summary>
        public const string Dapr = "dapr";

        /// <summary>
        /// Marks a channel that is not a message transport (storage, deferral,
        /// recovery, ...). Exporters exclude channels with this transport from
        /// the produced document.
        /// </summary>
        public const string Other = "other";
    }
}