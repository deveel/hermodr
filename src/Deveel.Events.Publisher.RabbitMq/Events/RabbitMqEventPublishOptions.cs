//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// Configuration options for a <see cref="RabbitMqEventPublishChannel"/>.
    /// </summary>
    public class RabbitMqEventPublishOptions : EventPublishOptions
    {
        /// <summary>
        /// Merges <paramref name="baseOptions"/> with <paramref name="typedOptions"/>,
        /// where every non-<c>null</c> property in <paramref name="typedOptions"/>
        /// overrides the corresponding property from <paramref name="baseOptions"/>.
        /// </summary>
        public static RabbitMqEventPublishOptions Merge(
            RabbitMqEventPublishOptions baseOptions,
            RabbitMqEventPublishOptions typedOptions)
        {
            return new RabbitMqEventPublishOptions
            {
                ConnectionString      = typedOptions.ConnectionString      ?? baseOptions.ConnectionString,
                ExchangeName          = typedOptions.ExchangeName          ?? baseOptions.ExchangeName,
                RoutingKey            = typedOptions.RoutingKey            ?? baseOptions.RoutingKey,
                QueueName             = typedOptions.QueueName             ?? baseOptions.QueueName,
                ClientName            = typedOptions.ClientName            ?? baseOptions.ClientName,
                JsonSerializerOptions = typedOptions.JsonSerializerOptions ?? baseOptions.JsonSerializerOptions,
                MessageFormat         = typedOptions.MessageFormat         ?? baseOptions.MessageFormat,
                MessageContent        = typedOptions.MessageContent        ?? baseOptions.MessageContent,
                PersistentMessages    = typedOptions.PersistentMessages    ?? baseOptions.PersistentMessages,
                PublisherConfirms     = typedOptions.PublisherConfirms     ?? baseOptions.PublisherConfirms,
                ConfirmTimeout        = typedOptions.ConfirmTimeout        ?? baseOptions.ConfirmTimeout,
                Mandatory             = typedOptions.Mandatory             ?? baseOptions.Mandatory,
            };
        }


        /// <summary>
        /// The default exchange name to publish the events to,
        /// when not explicitly defined in the event.
        /// </summary>
        public string? ExchangeName { get; set; }

        /// <summary>
        /// The default routing key to use when publishing the events,
        /// when not explicitly defined in the event.
        /// </summary>
        public string? RoutingKey { get; set; }

        /// <summary>
        /// The name of the queue to bind the exchange to.
        /// </summary>
        public string? QueueName { get; set; }

        /// <summary>
        /// The connection string to the RabbitMQ server.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// An optional client name to identify this application in RabbitMQ management UI.
        /// Defaults to <c>"Deveel.Events"</c>.
        /// </summary>
        public string? ClientName { get; set; }

        /// <summary>
        /// A set of options to configure the JSON serialization
        /// when the message format is set to <see cref="RabbitMqMessageFormat.Json"/>.
        /// </summary>
        public JsonSerializerOptions? JsonSerializerOptions { get; set; } = new JsonSerializerOptions();

        /// <summary>
        /// The format of the message to be published to the RabbitMQ queue.
        /// When <c>null</c> in a per-call override the channel default is used;
        /// the effective default is <see cref="RabbitMqMessageFormat.Json"/>.
        /// </summary>
        public RabbitMqMessageFormat? MessageFormat { get; set; }

        /// <summary>
        /// The type of content to be published to the RabbitMQ queue.
        /// When <c>null</c> in a per-call override the channel default is used;
        /// the effective default is <see cref="RabbitMqMessageContent.CloudEvent"/>.
        /// </summary>
        public RabbitMqMessageContent? MessageContent { get; set; }

        /// <summary>
        /// When <c>true</c>, messages are published with delivery mode set to persistent
        /// (delivery mode 2), ensuring they survive broker restarts.
        /// When <c>null</c> in a per-call override the channel default is used;
        /// the effective default is <c>true</c>.
        /// </summary>
        public bool? PersistentMessages { get; set; }

        /// <summary>
        /// When <c>true</c>, publisher confirms are enabled on the channel, so each
        /// publish waits for a broker acknowledgement before returning.
        /// When <c>null</c> in a per-call override the channel default is used;
        /// the effective default is <c>true</c>.
        /// </summary>
        public bool? PublisherConfirms { get; set; }

        /// <summary>
        /// The maximum time to wait for a publisher confirm from the broker.
        /// When <c>null</c> in a per-call override the channel default is used;
        /// the effective default is 5 seconds.
        /// </summary>
        public TimeSpan? ConfirmTimeout { get; set; }

        /// <summary>
        /// When <c>true</c>, the <c>mandatory</c> flag is set on published messages,
        /// causing the broker to return unroutable messages instead of silently discarding them.
        /// When <c>null</c> in a per-call override the channel default is used;
        /// the effective default is <c>false</c>.
        /// </summary>
        public bool? Mandatory { get; set; }
    }
}
