//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    /// <summary>
    /// Options that configure the <see cref="DaprPublishChannel"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Dapr publish channel maps CloudEvents onto a Dapr pub/sub component
    /// (identified by <see cref="PubSubName"/>) and a target topic
    /// (<see cref="Topic"/>). The underlying broker (Kafka, RabbitMQ, Azure
    /// Service Bus, NATS, ...) is selected through Dapr component configuration,
    /// keeping the publish code agnostic of the transport.
    /// </para>
    /// <para>
    /// When neither <see cref="Topic"/> nor <see cref="TopicSelector"/> is set,
    /// the channel falls back to the CloudEvent <c>subject</c> attribute as the
    /// topic, throwing at publish time when <c>subject</c> is also <c>null</c>.
    /// </para>
    /// </remarks>
    public class DaprPublishOptions : NamedChannelPublishOptions, INamedChannelFilter, IChannelMetadataSource
    {
        /// <summary>
        /// Merges <paramref name="baseOptions"/> with <paramref name="typedOptions"/>,
        /// where every non-<c>null</c> property in <paramref name="typedOptions"/>
        /// overrides the corresponding property from <paramref name="baseOptions"/>.
        /// </summary>
        /// <param name="baseOptions">The base (channel-level) options to merge from.</param>
        /// <param name="typedOptions">The typed per-event overrides to apply.</param>
        /// <returns>A new <see cref="DaprPublishOptions"/> with the merged values.</returns>
        public static DaprPublishOptions Merge(
            DaprPublishOptions baseOptions,
            DaprPublishOptions typedOptions)
        {
            return new DaprPublishOptions
            {
                ChannelName            = typedOptions.ChannelName            ?? baseOptions.ChannelName,
                PubSubName             = !string.IsNullOrEmpty(typedOptions.PubSubName) ? typedOptions.PubSubName : baseOptions.PubSubName,
                Topic                  = typedOptions.Topic                  ?? baseOptions.Topic,
                TopicSelector          = typedOptions.TopicSelector          ?? baseOptions.TopicSelector,
                MapAttributesToMetadata= typedOptions.MapAttributesToMetadata?? baseOptions.MapAttributesToMetadata,
                MetadataPrefix         = typedOptions.MetadataPrefix         ?? baseOptions.MetadataPrefix,
                DaprClientName         = typedOptions.DaprClientName         ?? baseOptions.DaprClientName,
                ScheduleDeliveryAt     = typedOptions.ScheduleDeliveryAt     ?? baseOptions.ScheduleDeliveryAt,
            };
        }

        /// <summary>
        /// Gets or sets the name of the Dapr pub/sub component the events are
        /// published to (the <c>pubsubname</c> argument of the Dapr
        /// <c>PublishEvent</c> building block).
        /// </summary>
        [Required(AllowEmptyStrings = false,
            ErrorMessage = "A Dapr pub/sub component name must be provided.")]
        public string PubSubName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the topic on the Dapr pub/sub component the events are
        /// published to. When <c>null</c> the channel resolves the topic from
        /// <see cref="TopicSelector"/> and, failing that, from the CloudEvent
        /// <c>subject</c> attribute.
        /// </summary>
        public string? Topic { get; set; }

        /// <summary>
        /// Gets or sets a delegate that selects the topic for a given CloudEvent
        /// at publish time, overriding <see cref="Topic"/> when it returns a
        /// non-<c>null</c> value.
        /// </summary>
        public Func<CloudEvent, string?>? TopicSelector { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the CloudEvent attributes
        /// (id, type, source, time, datacontenttype, etc.) should be carried
        /// as Dapr metadata on the published message.
        /// When <c>null</c> in a per-call override the channel default is used;
        /// the effective default is <c>true</c>.
        /// </summary>
        public bool? MapAttributesToMetadata { get; set; }

        /// <summary>
        /// Gets or sets the prefix applied to CloudEvent attribute names when
        /// they are projected onto Dapr metadata (see
        /// <see cref="MapAttributesToMetadata"/>). Defaults to <c>"ce-"</c>.
        /// </summary>
        public string? MetadataPrefix { get; set; }

        /// <summary>
        /// Gets or sets an optional name used to resolve a named
        /// <see cref="Dapr.Client.DaprClient"/> through the registered
        /// <see cref="IDaprClientBuilder"/>, allowing mTLS / token-credential
        /// configuration of multiple sidecars via the standard Dapr SDK surface.
        /// When <c>null</c> the default (unnamed) client is used.
        /// </summary>
        public string? DaprClientName { get; set; }

        /// <inheritdoc/>
        IEventPublishChannelMetadata IChannelMetadataSource.GetChannelMetadata()
        {
            var properties = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["pubsubname"] = PubSubName,
                ["topic"] = Topic,
            };

            foreach (var key in properties.Keys.Where(k => string.IsNullOrEmpty(properties[k])).ToList())
                properties.Remove(key);

            return new EventPublishChannelMetadata(ChannelName, EventTransports.Dapr, properties);
        }
    }
}