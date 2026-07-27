//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Threading;

using Microsoft.OpenApi;

using Saunter.AsyncApiSchema.v2;

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

    internal static IReadOnlyList<IEventSchema> DiscoverSchemas(IReadOnlyList<string> assemblyPaths)
    {
        var assemblies = new List<Assembly>(assemblyPaths.Count);
        foreach (var path in assemblyPaths)
        {
            try
            {
                assemblies.Add(Assembly.LoadFrom(path));
            }
            catch (FileNotFoundException ex)
            {
                AnsiConsole.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
            catch (FileLoadException ex)
            {
                AnsiConsole.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
            catch (BadImageFormatException ex)
            {
                AnsiConsole.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                AnsiConsole.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                AnsiConsole.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
        }

        var discovery = new EventSchemaDiscovery();
        return discovery.DiscoverSchemas(assemblies.ToArray());
    }

    internal static async Task<IReadOnlyList<IEventPublishChannelMetadata>> ResolveChannelsAsync(ExportSettings settings)
    {
        // Priority 1: explicit --channels-file (JSON).
        if (!string.IsNullOrEmpty(settings.ChannelsFile))
        {
            try
            {
                return await ChannelsFileLoader.LoadAsync(settings.ChannelsFile);
            }
            catch (FileNotFoundException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to read --channels-file '{settings.ChannelsFile}': {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to read --channels-file '{settings.ChannelsFile}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to read --channels-file '{settings.ChannelsFile}': {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to read --channels-file '{settings.ChannelsFile}': {ex.Message}");
            }
            catch (IOException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to read --channels-file '{settings.ChannelsFile}': {ex.Message}");
            }
            catch (JsonException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to read --channels-file '{settings.ChannelsFile}': {ex.Message}");
            }
        }

        // Priority 2: --entry <assembly-qualified-type-name>::<static-method>
        // that returns IReadOnlyList<IEventPublishChannelMetadata>.
        if (!string.IsNullOrEmpty(settings.Entry))
        {
            try
            {
                return EntryChannelResolver.Resolve(settings.Entry);
            }
            catch (ArgumentException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to invoke --entry '{settings.Entry}': {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to invoke --entry '{settings.Entry}': {ex.Message}");
            }
            catch (TypeLoadException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to invoke --entry '{settings.Entry}': {ex.Message}");
            }
            catch (FileNotFoundException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to invoke --entry '{settings.Entry}': {ex.Message}");
            }
            catch (FileLoadException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to invoke --entry '{settings.Entry}': {ex.Message}");
            }
            catch (MissingMethodException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to invoke --entry '{settings.Entry}': {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to invoke --entry '{settings.Entry}': {ex.Message}");
            }
            catch (NotSupportedException ex)
            {
                AnsiConsole.WriteLine($"warning: failed to invoke --entry '{settings.Entry}': {ex.Message}");
            }
        }

        return Array.Empty<IEventPublishChannelMetadata>();
    }

    internal static async Task WriteDocumentAsync(
        ExportSettings settings,
        IReadOnlyList<IEventSchema> schemas,
        IReadOnlyList<IEventPublishChannelMetadata> channels)
    {
        if (settings.Output == OutputFormat.AsyncApi)
        {
            var builder = new AsyncApiDocumentBuilder()
                .WithTitle(settings.Title ?? "Hermodr Events")
                .WithVersion(settings.Version ?? "1.0")
                .WithSchemas(schemas)
                .WithChannels(channels);

            var document = builder.Build();
            var content = AsyncApiSerializer.Serialize(document, MapAsyncApiFormat(settings.DocumentFormat));

            await WriteFileAsync(settings.OutputPath, content);
        }
        else // OpenAPI 3.1
        {
            var builder = new OpenApiWebhookDocumentBuilder()
                .WithTitle(settings.Title ?? "Hermodr Events")
                .WithVersion(settings.Version ?? "1.0")
                .WithSchemas(schemas)
                .WithChannels(channels);

            var document = builder.Build();
            using var ms = new MemoryStream();
            OpenApiDocumentWriter.Serialize(ms, document, MapOpenApiFormat(settings.DocumentFormat));
            ms.Position = 0;
            using var reader = new StreamReader(ms, leaveOpen: true);
            var content = reader.ReadToEnd();
            await WriteFileAsync(settings.OutputPath, content);
        }

        AnsiConsole.WriteLine($"wrote {settings.OutputPath} ({settings.Output}, {settings.DocumentFormat})");
    }

    private static AsyncApiFormat MapAsyncApiFormat(DocumentFormat format)
        => format == DocumentFormat.Yaml ? AsyncApiFormat.Yaml : AsyncApiFormat.Json;

    private static OpenApiFormat MapOpenApiFormat(DocumentFormat format)
        => format == DocumentFormat.Yaml ? OpenApiFormat.Yaml : OpenApiFormat.Json;

    private static async Task WriteFileAsync(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, content);
    }
}

/// <summary>
/// Settings for the <c>hermodr-asyncapi</c> export command.
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
}

/// <summary>
/// Discovers [Event]-annotated types in the given assemblies and exports
/// an AsyncAPI 2.x or OpenAPI 3.1 document describing them.
/// </summary>
internal sealed class ExportCommand : AsyncCommand<ExportSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Assemblies.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]error: at least one --assembly <path> is required.[/]");
            return 1;
        }

        var schemas = Program.DiscoverSchemas(settings.Assemblies);
        if (schemas.Count == 0)
            AnsiConsole.MarkupLine("[yellow]warning: no [[Event]]-annotated types with a DataVersion were found in the given assemblies.[/]");

        var channels = await Program.ResolveChannelsAsync(settings);

        try
        {
            await Program.WriteDocumentAsync(settings, schemas, channels);
            return 0;
        }
        catch (IOException ex)
        {
            AnsiConsole.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException ex)
        {
            AnsiConsole.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (JsonException ex)
        {
            AnsiConsole.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }
}

internal enum OutputFormat { AsyncApi, OpenApi }
internal enum DocumentFormat { Json, Yaml }