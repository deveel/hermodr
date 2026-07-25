//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

using Microsoft.OpenApi;

using Saunter.AsyncApiSchema.v2;

namespace Hermodr.Tool;

/// <summary>
/// Entry point for the <c>hermodr-asyncapi</c> dotnet global tool.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);
        if (options == null)
            return ExitUsage();

        if (options.Help)
            return ExitUsage(showHelp: true);

        if (options.Assemblies.Count == 0)
        {
            Console.Error.WriteLine("error: at least one --assembly <path> is required.");
            return 1;
        }

        // 1. Discover event schemas from the given assemblies.
        var schemas = DiscoverSchemas(options.Assemblies);
        if (schemas.Count == 0)
            Console.Error.WriteLine("warning: no [Event]-annotated types with a DataVersion were found in the given assemblies.");

        // 2. Resolve channel metadata: from --channels-file, or from an entry
        //    point type that returns IReadOnlyList<IEventPublishChannelMetadata>.
        var channels = await ResolveChannelsAsync(options);

        // 3. Build the requested document and write it to --out.
        try
        {
            await WriteDocumentAsync(options, schemas, channels);
            return 0;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (System.Text.Json.JsonException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }

    private static IReadOnlyList<IEventSchema> DiscoverSchemas(IReadOnlyList<string> assemblyPaths)
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
                Console.Error.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
            catch (FileLoadException ex)
            {
                Console.Error.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
            catch (BadImageFormatException ex)
            {
                Console.Error.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                Console.Error.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine($"warning: could not load assembly '{path}': {ex.Message}");
            }
        }

        var discovery = new EventSchemaDiscovery();
        return discovery.DiscoverSchemas(assemblies.ToArray());
    }

    private static async Task<IReadOnlyList<IEventPublishChannelMetadata>> ResolveChannelsAsync(ToolOptions options)
    {
        // Priority 1: explicit --channels-file (JSON).
        if (!string.IsNullOrEmpty(options.ChannelsFile))
        {
            try
            {
                return await ChannelsFileLoader.LoadAsync(options.ChannelsFile);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"warning: failed to read --channels-file '{options.ChannelsFile}': {ex.Message}");
            }
        }

        // Priority 2: --entry <assembly-qualified-type-name>::<static-method>
        // that returns IReadOnlyList<IEventPublishChannelMetadata>.
        if (!string.IsNullOrEmpty(options.Entry))
        {
            try
            {
                return EntryChannelResolver.Resolve(options.Entry);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"warning: failed to invoke --entry '{options.Entry}': {ex.Message}");
            }
        }

        return Array.Empty<IEventPublishChannelMetadata>();
    }

    private static async Task WriteDocumentAsync(
        ToolOptions options,
        IReadOnlyList<IEventSchema> schemas,
        IReadOnlyList<IEventPublishChannelMetadata> channels)
    {
        if (string.IsNullOrEmpty(options.OutputPath))
            throw new InvalidOperationException("No --out <path> was provided.");

        if (options.Output == OutputFormat.AsyncApi)
        {
            var builder = new AsyncApiDocumentBuilder()
                .WithTitle(options.Title ?? "Hermodr Events")
                .WithVersion(options.Version ?? "1.0")
                .WithSchemas(schemas)
                .WithChannels(channels);

            var document = builder.Build();
            var content = AsyncApiSerializer.Serialize(document, MapAsyncApiFormat(options.DocumentFormat));

            await WriteFileAsync(options.OutputPath!, content);
        }
        else // OpenAPI 3.1
        {
            var builder = new OpenApiWebhookDocumentBuilder()
                .WithTitle(options.Title ?? "Hermodr Events")
                .WithVersion(options.Version ?? "1.0")
                .WithSchemas(schemas)
                .WithChannels(channels);

            var document = builder.Build();
            using var ms = new MemoryStream();
            OpenApiDocumentWriter.Serialize(ms, document, MapOpenApiFormat(options.DocumentFormat));
            ms.Position = 0;
            using var reader = new StreamReader(ms, leaveOpen: true);
            var content = reader.ReadToEnd();
            await WriteFileAsync(options.OutputPath!, content);
        }

        Console.WriteLine($"wrote {options.OutputPath} ({options.Output}, {options.DocumentFormat})");
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

    // ── Argument parsing ────────────────────────────────────────────────────

    private static ToolOptions? ParseArgs(string[] args)
    {
        var opts = new ToolOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    opts.Help = true;
                    return opts;
                case "--assembly":
                    if (++i >= args.Length) return null!;
                    opts.Assemblies.Add(args[i]);
                    break;
                case "--output":
                    if (++i >= args.Length) return null!;
                    opts.Output = args[i].ToLowerInvariant() switch
                    {
                        "asyncapi" => OutputFormat.AsyncApi,
                        "openapi" => OutputFormat.OpenApi,
                        _ => OutputFormat.AsyncApi
                    };
                    break;
                case "--format":
                    if (++i >= args.Length) return null!;
                    opts.DocumentFormat = args[i].ToLowerInvariant() switch
                    {
                        "yaml" => DocumentFormat.Yaml,
                        "json" => DocumentFormat.Json,
                        _ => DocumentFormat.Json
                    };
                    break;
                case "--title":
                    if (++i >= args.Length) return null!;
                    opts.Title = args[i];
                    break;
                case "--version":
                    if (++i >= args.Length) return null!;
                    opts.Version = args[i];
                    break;
                case "--out":
                    if (++i >= args.Length) return null!;
                    opts.OutputPath = args[i];
                    break;
                case "--channels-file":
                    if (++i >= args.Length) return null!;
                    opts.ChannelsFile = args[i];
                    break;
                case "--entry":
                    if (++i >= args.Length) return null!;
                    opts.Entry = args[i];
                    break;
                default:
                    Console.Error.WriteLine($"error: unknown argument '{arg}'.");
                    return null;
            }
        }
        return opts;
    }

    private static int ExitUsage(bool showHelp = false)
    {
        Console.Error.WriteLine("""
        Usage: hermodr-asyncapi --assembly <path> [--assembly <path> ...]
                               [--output asyncapi|openapi]
                               [--format json|yaml]
                               [--title <title>] [--version <version>]
                               [--out <path>]
                               [--channels-file <path>]
                               [--entry <type>::<method>]

        Discovers [Event]-annotated types in the given assemblies and exports
        an AsyncAPI 2.x or OpenAPI 3.1 document describing them.

        Options:
          --assembly <path>       Assembly to scan (repeatable).
          --output <format>       asyncapi (default) | openapi
          --format <format>       json (default) | yaml
          --title <title>         Document info.title (default: 'Hermodr Events')
          --version <version>     Document info.version (default: '1.0')
          --out <path>            Output file path (required).
          --channels-file <path>  JSON file describing channel bindings:
                                   { "channels": [
                                     { "name": "...", "transport": "rabbitmq",
                                       "properties": { "exchange": "..." } }
                                   ] }
          --entry <type>::<method>  Assembly-qualified type name and a static
                                   method returning IReadOnlyList<IEventPublishChannelMetadata>.
          --help, -h              Show this help.
        """);
        return showHelp ? 0 : 1;
    }
}

internal sealed class ToolOptions
{
    public bool Help { get; set; }
    public List<string> Assemblies { get; } = new();
    public OutputFormat Output { get; set; } = OutputFormat.AsyncApi;
    public DocumentFormat DocumentFormat { get; set; } = DocumentFormat.Json;
    public string? Title { get; set; }
    public string? Version { get; set; }
    public string? OutputPath { get; set; }
    public string? ChannelsFile { get; set; }
    public string? Entry { get; set; }
}

internal enum OutputFormat { AsyncApi, OpenApi }
internal enum DocumentFormat { Json, Yaml }