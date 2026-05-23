//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Saunter.AsyncApiSchema.v2;

namespace Hermodr {
    /// <summary>
    /// Writes multiple <see cref="IEventSchema"/> instances into a single AsyncAPI 2.x
    /// document using the Saunter library, registering each schema under
    /// <c>components/schemas</c> and each message under <c>components/messages</c>.
    /// </summary>
    public sealed class EventSchemasAsyncApiWriter {
        /// <summary>
        /// Creates a new writer.
        /// </summary>
        /// <param name="title">The title for the AsyncAPI document's <c>info</c> block.</param>
        /// <param name="version">The version for the <c>info</c> block.</param>
        /// <param name="format">
        /// The output format. Defaults to <see cref="AsyncApiFormat.Json"/>.
        /// </param>
        public EventSchemasAsyncApiWriter(
            string title,
            string version,
            AsyncApiFormat format = AsyncApiFormat.Json) {
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
        public AsyncApiFormat Format { get; }

        /// <summary>
        /// Writes all <paramref name="schemas"/> into a single AsyncAPI document
        /// and serialises it to <paramref name="stream"/>.
        /// </summary>
        public async Task WriteToAsync(
            Stream stream,
            IEnumerable<IEventSchema> schemas,
            CancellationToken cancellationToken = default) {
            var document = BuildDocument(schemas);
            var content = AsyncApiSerializer.Serialize(document, Format);

            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await writer.WriteAsync(content);
        }

        private AsyncApiDocument BuildDocument(IEnumerable<IEventSchema> schemas) {
            var doc = new AsyncApiDocument {
                Info = new Info(Title, Version),
                Components = new Components()
            };

            foreach (var schema in schemas)
                doc.AddSchema(schema);

            return doc;
        }
    }
}