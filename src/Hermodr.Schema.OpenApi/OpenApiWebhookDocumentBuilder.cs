//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Hermodr
{
    /// <summary>
    /// Builds an OpenAPI 3.1 document whose <c>webhooks</c> section exposes one
    /// webhook entry per event type, each carrying a <c>POST</c> operation whose
    /// request body references the event schema under <c>components/schemas</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenAPI 3.1 has no concept of channel bindings; the transport identifier
    /// carried by <see cref="IEventPublishChannelMetadata"/> is preserved on the
    /// webhook operation via the <see cref="OpenApiOperation.Extensions"/> map
    /// under the <c>x-hermodr-transport</c> key so the export remains lossless.
    /// </para>
    /// <para>
    /// Channels whose <see cref="IEventPublishChannelMetadata.Transport"/> equals
    /// <see cref="EventTransports.Other"/> (storage / deferral / recovery layers)
    /// are excluded from the produced document.
    /// </para>
    /// </remarks>
    public sealed class OpenApiWebhookDocumentBuilder
    {
        private string? _title;
        private string? _version;
        private string? _description;
        private readonly List<IEventSchema> _schemas = new();
        private readonly List<IEventPublishChannelMetadata> _channels = new();

        /// <summary>
        /// Sets the document title (the <c>info.title</c> field). Required.
        /// </summary>
        public OpenApiWebhookDocumentBuilder WithTitle(string title)
        {
            ArgumentNullException.ThrowIfNull(title);
            _title = title;
            return this;
        }

        /// <summary>
        /// Sets the document version (the <c>info.version</c> field). Required.
        /// </summary>
        public OpenApiWebhookDocumentBuilder WithVersion(string version)
        {
            ArgumentNullException.ThrowIfNull(version);
            _version = version;
            return this;
        }

        /// <summary>
        /// Sets an optional description for the <c>info</c> block.
        /// </summary>
        public OpenApiWebhookDocumentBuilder WithDescription(string? description)
        {
            _description = description;
            return this;
        }

        /// <summary>
        /// Adds the given schemas to the document. Schema discovery is the
        /// caller's responsibility; see <see cref="EventSchemaDiscovery"/>.
        /// </summary>
        public OpenApiWebhookDocumentBuilder WithSchemas(IEnumerable<IEventSchema> schemas)
        {
            ArgumentNullException.ThrowIfNull(schemas);
            _schemas.AddRange(schemas);
            return this;
        }

        /// <summary>
        /// Adds the given channel metadata to the document. Channels whose
        /// transport is <see cref="EventTransports.Other"/> are ignored.
        /// </summary>
        public OpenApiWebhookDocumentBuilder WithChannels(IEnumerable<IEventPublishChannelMetadata> channels)
        {
            ArgumentNullException.ThrowIfNull(channels);
            _channels.AddRange(channels.Where(c =>
                c is not null &&
                !string.Equals(c.Transport, EventTransports.Other, StringComparison.Ordinal)));
            return this;
        }

        /// <summary>
        /// Builds the <see cref="OpenApiDocument"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="WithTitle"/> or <see cref="WithVersion"/> has not
        /// been called.
        /// </exception>
        public OpenApiDocument Build()
        {
            if (string.IsNullOrEmpty(_title))
                throw new InvalidOperationException("OpenAPI document title is required. Call WithTitle(...).");
            if (string.IsNullOrEmpty(_version))
                throw new InvalidOperationException("OpenAPI document version is required. Call WithVersion(...).");

            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo
                {
                    Title = _title!,
                    Version = _version!,
                    Description = _description
                },
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                },
                Webhooks = new Dictionary<string, IOpenApiPathItem>(StringComparer.Ordinal)
            };

            // Add schemas first so webhook request-body references resolve.
            foreach (var schema in _schemas)
            {
                var key = SanitizeKey(schema.EventType);
                doc.Components.Schemas[key] = schema.ToOpenApiSchema();
            }

            // Build a quick lookup of channel metadata by sanitized event-type
            // key. When multiple channels share the same event type the first one
            // wins (deterministic ordering preserved by insertion order).
            var channelByKey = new Dictionary<string, IEventPublishChannelMetadata>(StringComparer.Ordinal);
            foreach (var channel in _channels)
            {
                var key = string.IsNullOrEmpty(channel.ChannelName)
                    ? channel.Transport
                    : SanitizeKey(channel.ChannelName!);
                if (!channelByKey.ContainsKey(key))
                    channelByKey[key] = channel;
            }

            // One webhook per event type. When a matching channel exists, the
            // webhook operation carries the transport as an extension; otherwise
            // a transport-less webhook is emitted (schema-only contract).
            foreach (var schema in _schemas)
            {
                var key = SanitizeKey(schema.EventType);

                var operation = new OpenApiOperation
                {
                    OperationId = $"publish_{key}",
                    Summary = schema.Description,
                    Description = $"Publish the {schema.EventType} event",
                    RequestBody = new OpenApiRequestBody
                    {
                        Description = schema.Description,
                        Required = true,
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchemaReference(key, doc)
                            }
                        }
                    },
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "Event accepted" },
                        ["4xx"] = new OpenApiResponse { Description = "Client error" },
                        ["5xx"] = new OpenApiResponse { Description = "Server error" }
                    }
                };

                if (channelByKey.TryGetValue(key, out var channel))
                {
                    operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    operation.Extensions["x-hermodr-transport"] =
                        new Microsoft.OpenApi.JsonNodeExtension(
                            System.Text.Json.JsonSerializer.SerializeToNode(channel.Transport));
                    if (channel.Properties.Count > 0)
                        operation.Extensions["x-hermodr-properties"] =
                            new Microsoft.OpenApi.JsonNodeExtension(
                                System.Text.Json.JsonSerializer.SerializeToNode(channel.Properties));
                }

                doc.Webhooks[key] = new OpenApiPathItem
                {
                    Description = schema.Description,
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = operation
                    }
                };
            }

            return doc;
        }

        private static string SanitizeKey(string value)
            => value.Replace('.', '-').Replace('/', '-').Replace(' ', '-');
    }
}