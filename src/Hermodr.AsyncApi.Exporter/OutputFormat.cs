//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// The top-level document format produced by the exporter.
/// </summary>
public enum OutputFormat
{
    /// <summary>AsyncAPI 2.x document (default).</summary>
    AsyncApi,
    /// <summary>OpenAPI 3.1 webhook document.</summary>
    OpenApi
}

/// <summary>
/// The serialisation format of the produced document.
/// </summary>
public enum DocumentFormat
{
    /// <summary>JSON serialisation (default).</summary>
    Json,
    /// <summary>YAML serialisation.</summary>
    Yaml
}