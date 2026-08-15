//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.OpenApi;

namespace Hermodr
{
    /// <summary>
    /// Shared serialisation helpers for <see cref="OpenApiDocument"/> objects,
    /// producing JSON or YAML output via the Microsoft.OpenApi writers.
    /// </summary>
    public static class OpenApiDocumentWriter
    {
        /// <summary>Serialise the supplied OpenAPI document to the supplied stream in the requested format.</summary>
        /// <param name="stream">The destination stream to write the serialised document to.</param>
        /// <param name="document">The OpenAPI document to serialise.</param>
        /// <param name="format">The serialisation format (JSON or YAML).</param>
        /// <remarks>
        /// The format switches between the <see cref="OpenApiYamlWriter"/> and <see cref="OpenApiJsonWriter"/>
        /// implementations from Microsoft.OpenApi.
        /// </remarks>
        public static void Serialize(Stream stream, OpenApiDocument document, OpenApiFormat format)
        {
            using var textWriter = new StreamWriter(stream, leaveOpen: true);
            IOpenApiWriter writer = format == OpenApiFormat.Yaml
                ? new OpenApiYamlWriter(textWriter)
                : new OpenApiJsonWriter(textWriter);

            document.SerializeAs(OpenApiSpecVersion.OpenApi3_1, writer);
        }
    }
}