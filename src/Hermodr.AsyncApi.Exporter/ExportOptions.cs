//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Plain options bag carrying the inputs of an export operation. Decoupled
/// from any CLI parsing framework so the same orchestration can be driven from
/// tests, MSBuild tasks, or the dotnet global tool.
/// </summary>
public sealed class ExportOptions
{
    /// <summary>
    /// Absolute or relative paths to the assemblies to scan for
    /// <c>[Event]</c>-annotated types. At least one must be supplied.
    /// </summary>
    public IReadOnlyList<string> Assemblies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The top-level document format to produce. Defaults to
    /// <see cref="OutputFormat.AsyncApi"/>.
    /// </summary>
    public OutputFormat Output { get; init; } = OutputFormat.AsyncApi;

    /// <summary>
    /// The serialisation format of the produced document. Defaults to
    /// <see cref="DocumentFormat.Json"/>.
    /// </summary>
    public DocumentFormat DocumentFormat { get; init; } = DocumentFormat.Json;

    /// <summary>
    /// The <c>info.title</c> of the produced document. Defaults to
    /// <c>"Hermodr Events"</c> when unset.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The <c>info.version</c> of the produced document. Defaults to
    /// <c>"1.0"</c> when unset.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// The output file path. Required.
    /// </summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>
    /// Optional path to a JSON file describing channel bindings (see
    /// <see cref="ChannelMetadataFileLoader"/> for the schema).
    /// </summary>
    public string? ChannelsFile { get; init; }

    /// <summary>
    /// Optional entry-point specifier in the form
    /// <c>&lt;assembly-qualified-type-name&gt;::&lt;static-method&gt;</c> whose
    /// method returns <see cref="IReadOnlyList{T}"/> of
    /// <see cref="IEventPublishChannelMetadata"/>. See
    /// <see cref="EntryChannelResolver"/>.
    /// </summary>
    public string? Entry { get; init; }
}