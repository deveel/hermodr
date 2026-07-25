//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.ObjectModel;

using Saunter.AsyncApiSchema.v2;
using Saunter.AsyncApiSchema.v2.Bindings;
using Saunter.AsyncApiSchema.v2.Bindings.Amqp;
using Saunter.AsyncApiSchema.v2.Bindings.Http;

namespace Hermodr
{
    /// <summary>
    /// Builds an enriched <see cref="AsyncApiDocument"/> from discovered event
    /// schemas and registered publish-channel metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This builder is the export-time entry point for AsyncAPI 2.x document
    /// generation. It chains schema discovery (<see cref="EventSchemaDiscovery"/>),
    /// transport-aware channel/operation population (from
    /// <see cref="IEventPublishChannelMetadata"/>), and server aggregation into a
    /// single document.
    /// </para>
    /// <para>
    /// Channels whose <see cref="IEventPublishChannelMetadata.Transport"/> is
    /// <see cref="EventTransports.Other"/> (storage / deferral / recovery layers)
    /// are excluded from the produced document.
    /// </para>
    /// </remarks>
    public sealed class AsyncApiDocumentBuilder
    {
        private string? _title;
        private string? _version;
        private string? _description;
        private readonly List<IEventSchema> _schemas = new();
        private readonly List<IEventPublishChannelMetadata> _channels = new();

        /// <summary>
        /// Sets the document title (the <c>info.title</c> field). Required.
        /// </summary>
        public AsyncApiDocumentBuilder WithTitle(string title)
        {
            ArgumentNullException.ThrowIfNull(title);
            _title = title;
            return this;
        }

        /// <summary>
        /// Sets the document version (the <c>info.version</c> field). Required.
        /// </summary>
        public AsyncApiDocumentBuilder WithVersion(string version)
        {
            ArgumentNullException.ThrowIfNull(version);
            _version = version;
            return this;
        }

        /// <summary>
        /// Sets an optional description for the <c>info</c> block.
        /// </summary>
        public AsyncApiDocumentBuilder WithDescription(string? description)
        {
            _description = description;
            return this;
        }

        /// <summary>
        /// Adds the given schemas to the document. Schema discovery is the
        /// caller's responsibility; see <see cref="EventSchemaDiscovery"/>.
        /// </summary>
        public AsyncApiDocumentBuilder WithSchemas(IEnumerable<IEventSchema> schemas)
        {
            ArgumentNullException.ThrowIfNull(schemas);
            _schemas.AddRange(schemas);
            return this;
        }

        /// <summary>
        /// Adds the given channel metadata to the document. Channels whose
        /// transport is <see cref="EventTransports.Other"/> are ignored.
        /// </summary>
        public AsyncApiDocumentBuilder WithChannels(IEnumerable<IEventPublishChannelMetadata> channels)
        {
            ArgumentNullException.ThrowIfNull(channels);
            foreach (var c in channels)
            {
                if (c is null) continue;
                if (string.Equals(c.Transport, EventTransports.Other, StringComparison.Ordinal))
                    continue;
                _channels.Add(c);
            }
            return this;
        }

        /// <summary>
        /// Builds the <see cref="AsyncApiDocument"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="WithTitle"/> or <see cref="WithVersion"/> has not
        /// been called.
        /// </exception>
        public AsyncApiDocument Build()
        {
            if (string.IsNullOrEmpty(_title))
                throw new InvalidOperationException("AsyncAPI document title is required. Call WithTitle(...).");
            if (string.IsNullOrEmpty(_version))
                throw new InvalidOperationException("AsyncAPI document version is required. Call WithVersion(...).");

            var doc = new AsyncApiDocument
            {
                Info = new Info(_title!, _version!)
                {
                    Description = _description
                },
                Components = new Components()
            };

            // Add schemas first so channel message references resolve.
            foreach (var schema in _schemas)
                doc.AddSchema(schema);

            // Add channel/operation bindings from transport metadata. When a
            // channel is associated with one or more event types (via the
            // ChannelName matching the sanitized event-type key), the
            // operation Message is left as a reference resolved by AddSchema.
            foreach (var channel in _channels)
                AddChannelToDocument(doc, channel);

            // Aggregate servers: one per distinct (url, protocol) pair observed
            // across channels. Named servers are keyed by a sanitised URL.
            foreach (var (serverName, server) in AggregateServers(_channels))
                doc.Servers[serverName] = server;

            return doc;
        }

        private static void AddChannelToDocument(AsyncApiDocument doc, IEventPublishChannelMetadata channel)
        {
            var key = string.IsNullOrEmpty(channel.ChannelName)
                ? channel.Transport
                : SanitizeKey(channel.ChannelName!);

            var channelItem = doc.Channels.TryGetValue(key, out var existing)
                ? existing
                : new ChannelItem();

            var binding = ResolveChannelBinding(channel);
            if (binding != null)
                channelItem.Bindings = binding;

            // Preserve any description set by AddSchema for schema-only channels.
            if (string.IsNullOrEmpty(channelItem.Description))
                channelItem.Description = $"Publish channel ({channel.Transport})";

            // Operation: a publish operation on this channel. The message
            // reference is left to be resolved by the schema-side AddSchema when
            // channel name and event type key match; otherwise a free-form
            // message carries the transport info.
            channelItem.Publish ??= new Operation
            {
                OperationId = $"publish_{key}",
                Summary = $"Publish to {channel.Transport} channel"
            };

            doc.Channels[key] = channelItem;
        }

        internal static IChannelBindings? ResolveChannelBinding(IEventPublishChannelMetadata channel)
        {
            if (string.Equals(channel.Transport, EventTransports.RabbitMq, StringComparison.Ordinal))
            {
                var amqp = new AmqpChannelBinding
                {
                    Is = AmqpChannelBindingIs.RoutingKey,
                    BindingVersion = "0.2.0"
                };

                if (channel.Properties.TryGetValue("exchange", out var exchange) && !string.IsNullOrEmpty(exchange))
                {
                    amqp.Exchange = new AmqpChannelBindingExchange
                    {
                        Name = exchange,
                        Type = AmqpChannelBindingExchangeType.Topic,
                        Durable = true,
                        AutoDelete = false
                    };
                }

                if (channel.Properties.TryGetValue("queue", out var queue) && !string.IsNullOrEmpty(queue))
                {
                    amqp.Queue = new AmqpChannelBindingQueue
                    {
                        Name = queue,
                        Durable = true,
                        Exclusive = false,
                        AutoDelete = false
                    };
                }

                return new ChannelBindings { Amqp = amqp };
            }

            if (string.Equals(channel.Transport, EventTransports.Webhook, StringComparison.Ordinal) ||
                string.Equals(channel.Transport, EventTransports.Http, StringComparison.Ordinal))
            {
                return new ChannelBindings { Http = new HttpChannelBinding() };
            }

            // Unknown transport (e.g. "kafka", "nats", "azureservicebus"):
            // no native AsyncAPI 2.x binding in Saunter. The transport identifier
            // is preserved in the channel Description (set above) so the export
            // remains lossless.
            return null;
        }

        private static IEnumerable<KeyValuePair<string, Server>> AggregateServers(
            IReadOnlyList<IEventPublishChannelMetadata> channels)
        {
            var seen = new Dictionary<string, Server>(StringComparer.Ordinal);

            foreach (var channel in channels)
            {
                if (string.Equals(channel.Transport, EventTransports.Other, StringComparison.Ordinal))
                    continue;

                var (url, protocol) = ResolveServerEndpoint(channel);
                if (url is null) continue;

                var name = SanitizeKey(url);
                if (seen.ContainsKey(name)) continue;

                seen[name] = new Server(url!, protocol)
                {
                    Description = $"{channel.Transport} server"
                };
            }

            return seen;
        }

        private static (string? url, string protocol) ResolveServerEndpoint(IEventPublishChannelMetadata channel)
        {
            // For HTTP-based transports, the per-channel url property (when
            // present) is the server URL.
            if (string.Equals(channel.Transport, EventTransports.Webhook, StringComparison.Ordinal) ||
                string.Equals(channel.Transport, EventTransports.Http, StringComparison.Ordinal))
            {
                if (channel.Properties.TryGetValue("url", out var url) && !string.IsNullOrEmpty(url))
                    return (url!, ServerProtocol.Https);
                return (null, ServerProtocol.Http);
            }

            // For other transports, the broker URL is not always exposed in the
            // options (e.g. RabbitMQ.ConnectionString / ASB.ConnectionString are
            // connection strings, not URLs). We surface them verbatim as the
            // server URL when available via the "url" property; otherwise no
            // server entry is produced.
            if (channel.Properties.TryGetValue("url", out var explicitUrl) && !string.IsNullOrEmpty(explicitUrl))
                return (explicitUrl!, ResolveProtocolForTransport(channel.Transport));

            return (null, ResolveProtocolForTransport(channel.Transport));
        }

        private static string ResolveProtocolForTransport(string transport)
        {
            // AsyncAPI 2.x ServerProtocol is a string; return the closest match
            // or the transport identifier itself for unknown values.
            return transport switch
            {
                EventTransports.RabbitMq => ServerProtocol.Amqp,
                EventTransports.AzureServiceBus => ServerProtocol.Amqps,
                EventTransports.Webhook => ServerProtocol.Https,
                EventTransports.Http => ServerProtocol.Http,
                _ => transport
            };
        }

        private static string SanitizeKey(string value)
            => value.Replace('.', '-').Replace('/', '-').Replace(' ', '-');
    }
}