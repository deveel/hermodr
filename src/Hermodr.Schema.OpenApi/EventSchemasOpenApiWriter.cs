//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.OpenApi;

namespace Hermodr
{
    /// <summary>
    /// Writes multiple <see cref="IEventSchema"/> instances into a single OpenAPI 3.1
    /// document whose <c>webhooks</c> section exposes one webhook entry per event
    /// type.
    /// </summary>
    public sealed class EventSchemasOpenApiWriter
    {
        /// <summary>
        /// Creates a new writer.
        /// </summary>
        /// <param name="title">The title for the OpenAPI document's <c>info</c> block.</param>
        /// <param name="version">The version for the <c>info</c> block.</param>
        /// <param name="format">
        /// The output format. Defaults to <see cref="OpenApiFormat.Json"/>.
        /// </param>
        public EventSchemasOpenApiWriter(
            string title,
            string version,
            OpenApiFormat format = OpenApiFormat.Json)
        {
            ArgumentNullException.ThrowIfNull(title, nameof(title));
            ArgumentNullException.ThrowIfNull(version, nameof(version));

            Title = title;
            Version = version;
            Format = format;
        }

        /// <summary>The document title.</summary>
        public string Title { get; }

        /// <summary>The document version.</summary>
        public string Version { get; }

        /// <summary>The serialisation format.</summary>
        public OpenApiFormat Format { get; }

        /// <summary>
        /// Writes all <paramref name="schemas"/> into a single OpenAPI document
        /// and serialises it to <paramref name="stream"/>.
        /// </summary>
        public async Task WriteToAsync(
            Stream stream,
            IEnumerable<IEventSchema> schemas,
            CancellationToken cancellationToken = default)
        {
            var document = new OpenApiWebhookDocumentBuilder()
                .WithTitle(Title)
                .WithVersion(Version)
                .WithSchemas(schemas)
                .Build();

            OpenApiDocumentWriter.Serialize(stream, document, Format);
            await Task.CompletedTask;
        }
    }
}