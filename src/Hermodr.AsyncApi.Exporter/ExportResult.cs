//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// The outcome of an export operation. The CLI layer is responsible for
/// rendering <see cref="Errors"/> and <see cref="Warnings"/> to the console;
/// the library itself never touches the terminal.
/// </summary>
public sealed class ExportResult
{
    /// <summary>
    /// <c>true</c> when the document was written to <see cref="OutputPath"/>;
    /// <c>false</c> when a validation or I/O error prevented it.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The process exit code to return. <c>0</c> on success, non-zero on
    /// validation / I/O errors (the orchestrator chooses distinct codes for
    /// distinct failure classes so the CLI can forward them verbatim).
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// The absolute path of the file that was written, or the value of
    /// <see cref="ExportOptions.OutputPath"/> unchanged when no file was
    /// produced.
    /// </summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>
    /// The format that was actually produced (useful when callers don't pass
    /// <see cref="ExportOptions.Output"/> explicitly and want to know which
    /// default was applied).
    /// </summary>
    public OutputFormat Output { get; init; }

    /// <summary>
    /// The serialisation format that was actually produced.
    /// </summary>
    public DocumentFormat DocumentFormat { get; init; }

    /// <summary>
    /// Non-fatal messages recorded during the export (e.g. an assembly that
    /// could not be loaded, a channel with <see cref="EventTransports.Other"/>
    /// that was skipped). The export still succeeds when only warnings are
    /// present.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Fatal error messages that prevented the export. When non-empty,
    /// <see cref="Success"/> is <c>false</c>.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}