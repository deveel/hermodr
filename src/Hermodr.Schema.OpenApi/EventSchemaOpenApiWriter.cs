//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.OpenApi;

namespace Hermodr
{
    /// <summary>
    /// An <see cref="IEventSchemaWriter"/> implementation that serialises an
    /// <see cref="IEventSchema"/> as a standalone OpenAPI 3.1 document whose
    /// <c>webhooks</c> section exposes a single webhook entry for the schema's
    /// event type.
    /// </summary>
    public sealed class EventSchemaOpenApiWriter : IEventSchemaWriter
    {
        /// <summary>
        /// Creates a new writer.
        /// </summary>
        /// <param name="format">
        /// The output format. Defaults to <see cref="OpenApiFormat.Json"/>.
        /// </param>
        /// <param name="title">
        /// An optional title for the OpenAPI document's <c>info</c> block.
        /// When <see langword="null"/> the event type is used.
        /// </param>
        /// <param name="documentVersion">
        /// An optional version string for the <c>info</c> block.
        /// When <see langword="null"/> the schema version is used.
        /// </param>
        public EventSchemaOpenApiWriter(
            OpenApiFormat format = OpenApiFormat.Json,
            string? title = null,
            string? documentVersion = null)
        {
            Format = format;
            Title = title;
            DocumentVersion = documentVersion;
        }

        /// <summary>The serialisation format (JSON or YAML).</summary>
        public OpenApiFormat Format { get; }

        /// <summary>
        /// Optional override for the document title in the <c>info</c> block.
        /// </summary>
        public string? Title { get; }

        /// <summary>
        /// Optional override for the document version in the <c>info</c> block.
        /// </summary>
        public string? DocumentVersion { get; }

        /// <inheritdoc/>
        public async Task WriteToAsync(
            Stream stream,
            IEventSchema schema,
            CancellationToken cancellationToken = default)
        {
            var builder = new OpenApiWebhookDocumentBuilder()
                .WithTitle(Title ?? schema.EventType)
                .WithVersion(DocumentVersion ?? schema.Version)
                .WithSchemas(new[] { schema });

            var document = builder.Build();
            OpenApiDocumentWriter.Serialize(stream, document, Format);
            await Task.CompletedTask;
        }
    }
}