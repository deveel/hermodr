//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// An <see cref="IEventSchemaWriter"/> implementation that serialises an
    /// <see cref="IEventSchema"/> as a standalone AsyncAPI 2.x document using
    /// the Saunter library.
    /// </summary>
    /// <remarks>
    /// The produced document exposes the schema under
    /// <c>components/schemas/{eventType}</c>, declares a corresponding message
    /// under <c>components/messages/{eventType}</c>, and wires a subscribe channel
    /// at <c>channels/{eventType}</c>.
    /// </remarks>
    public sealed class EventSchemaAsyncApiWriter : IEventSchemaWriter {
        /// <summary>
        /// Creates a new writer.
        /// </summary>
        /// <param name="format">
        /// The output format. Defaults to <see cref="AsyncApiFormat.Json"/>.
        /// </param>
        /// <param name="title">
        /// An optional title for the AsyncAPI document's <c>info</c> block.
        /// When <see langword="null"/> the event type is used.
        /// </param>
        /// <param name="documentVersion">
        /// An optional version string for the <c>info</c> block.
        /// When <see langword="null"/> the schema version is used.
        /// </param>
        public EventSchemaAsyncApiWriter(
            AsyncApiFormat format = AsyncApiFormat.Json,
            string? title = null,
            string? documentVersion = null) {
            Format = format;
            Title = title;
            DocumentVersion = documentVersion;
        }

        /// <summary>The serialisation format (JSON or YAML).</summary>
        public AsyncApiFormat Format { get; }

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
            CancellationToken cancellationToken = default) {
            var document = schema.ToAsyncApiDocument(Title, DocumentVersion);
            var content = AsyncApiSerializer.Serialize(document, Format);

            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await writer.WriteAsync(content);
        }
    }
}