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