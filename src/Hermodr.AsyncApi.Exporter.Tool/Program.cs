//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel;

using Spectre.Console;
using Spectre.Console.Cli;

namespace Hermodr.Tool;

/// <summary>
/// Entry point for the <c>hermodr-asyncapi</c> dotnet global tool.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp<ExportCommand>();
        app.Configure(config =>
        {
            config.SetApplicationName("hermodr-asyncapi");
            config.UseStrictParsing();
        });
        return await app.RunAsync(args);
    }
}

/// <summary>
/// Spectre.Console.Cli settings for the <c>export</c> command. Acts as a
/// thin shim over the console-agnostic <see cref="ExportOptions"/> POCO.
/// </summary>
internal sealed class ExportSettings : CommandSettings
{
    [CommandOption("--assembly <PATH>")]
    [Description("Assembly to scan for [[Event]]-annotated types (repeatable).")]
    public string[] Assemblies { get; init; } = Array.Empty<string>();

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: asyncapi (default) | openapi.")]
    [DefaultValue(OutputFormat.AsyncApi)]
    public OutputFormat Output { get; init; }

    [CommandOption("--format <FORMAT>")]
    [Description("Document format: json (default) | yaml.")]
    [DefaultValue(DocumentFormat.Json)]
    public DocumentFormat DocumentFormat { get; init; }

    [CommandOption("--title <TITLE>")]
    [Description("Document info.title (default: 'Hermodr Events').")]
    public string? Title { get; init; }

    [CommandOption("--version <VERSION>")]
    [Description("Document info.version (default: '1.0').")]
    public string? Version { get; init; }

    [CommandOption("--out <PATH>", isRequired: true)]
    [Description("Output file path.")]
    public string OutputPath { get; init; } = null!;

    [CommandOption("--channels-file <PATH>")]
    [Description("JSON file describing channel bindings.")]
    public string? ChannelsFile { get; init; }

    [CommandOption("--entry <ENTRY>")]
    [Description("Assembly-qualified type name and a static method, in the form '<type>::<method>', returning IReadOnlyList<IEventPublishChannelMetadata>.")]
    public string? Entry { get; init; }

    /// <summary>
    /// Maps these Spectre-bound settings to a console-agnostic
    /// <see cref="ExportOptions"/>.
    /// </summary>
    public ExportOptions ToOptions()
        => new()
        {
            Assemblies = Assemblies,
            Output = Output,
            DocumentFormat = DocumentFormat,
            Title = Title,
            Version = Version,
            OutputPath = OutputPath,
            ChannelsFile = ChannelsFile,
            Entry = Entry
        };
}

/// <summary>
/// Discovers [Event]-annotated types in the given assemblies and exports
/// an AsyncAPI 2.x or OpenAPI 3.1 document describing them.
/// </summary>
internal sealed class ExportCommand : AsyncCommand<ExportSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        ExportSettings settings,
        CancellationToken cancellationToken)
    {
        var result = await ExportService.ExportAsync(settings.ToOptions(), cancellationToken);

        foreach (var w in result.Warnings)
            AnsiConsole.MarkupLineInterpolated($"[yellow]warning:[/] {w}");

        if (result.Success)
        {
            AnsiConsole.MarkupLineInterpolated(
                $"wrote {result.OutputPath} ({result.Output}, {result.DocumentFormat})");
            return result.ExitCode;
        }

        foreach (var e in result.Errors)
            AnsiConsole.MarkupLineInterpolated($"[red]error:[/] {e}");

        return result.ExitCode;
    }
}