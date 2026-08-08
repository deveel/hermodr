//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Reflection;
using System.Text.Json;

using Microsoft.OpenApi;

using Saunter.AsyncApiSchema.v2;

namespace Hermodr;

/// <summary>
/// Orchestrates an export operation: discovers <c>[Event]</c>-annotated
/// schemas from the given assemblies, resolves channel metadata from
/// <see cref="ExportOptions.ChannelsFile"/> and/or <see cref="ExportOptions.Entry"/>,
/// builds an AsyncAPI 2.x or OpenAPI 3.1 document, and writes it to
/// <see cref="ExportOptions.OutputPath"/>.
/// </summary>
/// <remarks>
/// <para>
/// This service is console-agnostic: it records failures in
/// <see cref="ExportResult.Errors"/> / <see cref="ExportResult.Warnings"/>
/// rather than writing to the terminal. The CLI layer is responsible for
/// rendering them.
/// </para>
/// <para>
/// The discovery step uses reflection and is therefore not suitable for
/// trimmed / AOT-compiled deployments.
/// </para>
/// </remarks>
public static class ExportService
{
    private const int ExitValidation = 1;
    private const int ExitIoFailure = 2;

    /// <summary>
    /// Runs an export operation for the given <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The export inputs.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous write operation.
    /// </param>
    /// <returns>
    /// An <see cref="ExportResult"/> describing the outcome. Never throws for
    /// validation, file-load, or I/O failures — they are captured in
    /// <see cref="ExportResult.Errors"/>. Programming bugs (NRE, etc.) still
    /// propagate.
    /// </returns>
    public static async Task<ExportResult> ExportAsync(
        ExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var warnings = new List<string>();
        var errors = new List<string>();

        if (options.Assemblies.Count == 0)
        {
            errors.Add("at least one --assembly <path> is required.");
            return Failure(options, errors, warnings, ExitValidation);
        }

        var schemas = DiscoverSchemas(options.Assemblies, warnings);
        if (schemas.Count == 0)
            warnings.Add("no [Event]-annotated types with a DataVersion were found in the given assemblies.");

        var channels = await ResolveChannelsAsync(options, warnings, cancellationToken);

        try
        {
            await WriteDocumentAsync(options, schemas, channels, cancellationToken);
        }
        catch (IOException ex)
        {
            errors.Add(ex.Message);
            return Failure(options, errors, warnings, ExitIoFailure);
        }
        catch (UnauthorizedAccessException ex)
        {
            errors.Add(ex.Message);
            return Failure(options, errors, warnings, ExitIoFailure);
        }
        catch (JsonException ex)
        {
            errors.Add(ex.Message);
            return Failure(options, errors, warnings, ExitIoFailure);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
            return Failure(options, errors, warnings, ExitIoFailure);
        }
        catch (ArgumentException ex)
        {
            errors.Add(ex.Message);
            return Failure(options, errors, warnings, ExitValidation);
        }

        return new ExportResult
        {
            Success = true,
            ExitCode = 0,
            OutputPath = options.OutputPath,
            Output = options.Output,
            DocumentFormat = options.DocumentFormat,
            Warnings = warnings,
            Errors = Array.Empty<string>()
        };
    }

    /// <summary>
    /// Loads the given assemblies and discovers <c>[Event]</c>-annotated
    /// types from them. Assembly-load failures are recorded as warnings and
    /// skipped; the call never throws for a single bad assembly.
    /// </summary>
    public static IReadOnlyList<IEventSchema> DiscoverSchemas(
        IReadOnlyList<string> assemblyPaths,
        IList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(assemblyPaths);
        ArgumentNullException.ThrowIfNull(warnings);

        var assemblies = new List<Assembly>(assemblyPaths.Count);
        foreach (var path in assemblyPaths)
        {
            try
            {
                assemblies.Add(Assembly.LoadFrom(path));
            }
            catch (FileNotFoundException ex)
            {
                warnings.Add($"could not load assembly '{path}': {ex.Message}");
            }
            catch (FileLoadException ex)
            {
                warnings.Add($"could not load assembly '{path}': {ex.Message}");
            }
            catch (BadImageFormatException ex)
            {
                warnings.Add($"could not load assembly '{path}': {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                warnings.Add($"could not load assembly '{path}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                warnings.Add($"could not load assembly '{path}': {ex.Message}");
            }
        }

        var discovery = new EventSchemaDiscovery();
        return discovery.DiscoverSchemas(assemblies.ToArray());
    }

    /// <summary>
    /// Resolves channel metadata from <see cref="ExportOptions.ChannelsFile"/>
    /// (priority 1) and <see cref="ExportOptions.Entry"/> (priority 2). Both
    /// may be supplied; the channels file is tried first. Failures are
    /// recorded as warnings and skipped — the export continues with an empty
    /// channel set.
    /// </summary>
    public static async Task<IReadOnlyList<IEventPublishChannelMetadata>> ResolveChannelsAsync(
        ExportOptions options,
        IList<string> warnings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(warnings);

        // Priority 1: explicit --channels-file (JSON).
        if (!string.IsNullOrEmpty(options.ChannelsFile))
        {
            try
            {
                var loaded = await ChannelMetadataFileLoader.LoadAsync(
                    options.ChannelsFile, cancellationToken);
                foreach (var w in loaded.Warnings)
                    warnings.Add(w);
                return loaded.Channels;
            }
            catch (FileNotFoundException ex)
            {
                warnings.Add($"failed to read --channels-file '{options.ChannelsFile}': {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                warnings.Add($"failed to read --channels-file '{options.ChannelsFile}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                warnings.Add($"failed to read --channels-file '{options.ChannelsFile}': {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                warnings.Add($"failed to read --channels-file '{options.ChannelsFile}': {ex.Message}");
            }
            catch (IOException ex)
            {
                warnings.Add($"failed to read --channels-file '{options.ChannelsFile}': {ex.Message}");
            }
            catch (JsonException ex)
            {
                warnings.Add($"failed to read --channels-file '{options.ChannelsFile}': {ex.Message}");
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
            catch (ArgumentException ex)
            {
                warnings.Add($"failed to invoke --entry '{options.Entry}': {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                warnings.Add($"failed to invoke --entry '{options.Entry}': {ex.Message}");
            }
            catch (TypeLoadException ex)
            {
                warnings.Add($"failed to invoke --entry '{options.Entry}': {ex.Message}");
            }
            catch (FileNotFoundException ex)
            {
                warnings.Add($"failed to invoke --entry '{options.Entry}': {ex.Message}");
            }
            catch (FileLoadException ex)
            {
                warnings.Add($"failed to invoke --entry '{options.Entry}': {ex.Message}");
            }
            catch (MissingMethodException ex)
            {
                warnings.Add($"failed to invoke --entry '{options.Entry}': {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
                warnings.Add($"failed to invoke --entry '{options.Entry}': {ex.Message}");
            }
            catch (NotSupportedException ex)
            {
                warnings.Add($"failed to invoke --entry '{options.Entry}': {ex.Message}");
            }
        }

        return Array.Empty<IEventPublishChannelMetadata>();
    }

    /// <summary>
    /// Builds the AsyncAPI or OpenAPI document and writes it to
    /// <see cref="ExportOptions.OutputPath"/>.
    /// </summary>
    public static async Task WriteDocumentAsync(
        ExportOptions options,
        IReadOnlyList<IEventSchema> schemas,
        IReadOnlyList<IEventPublishChannelMetadata> channels,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(schemas);
        ArgumentNullException.ThrowIfNull(channels);

        if (options.Output == OutputFormat.AsyncApi)
        {
            var builder = new AsyncApiDocumentBuilder()
                .WithTitle(options.Title ?? "Hermodr Events")
                .WithVersion(options.Version ?? "1.0")
                .WithSchemas(schemas)
                .WithChannels(channels);

            var document = builder.Build();
            var content = AsyncApiSerializer.Serialize(document, MapAsyncApiFormat(options.DocumentFormat));

            await WriteFileAsync(options.OutputPath, content, cancellationToken);
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
            var content = await reader.ReadToEndAsync(cancellationToken);
            await WriteFileAsync(options.OutputPath, content, cancellationToken);
        }
    }

    private static AsyncApiFormat MapAsyncApiFormat(DocumentFormat format)
        => format == DocumentFormat.Yaml ? AsyncApiFormat.Yaml : AsyncApiFormat.Json;

    private static OpenApiFormat MapOpenApiFormat(DocumentFormat format)
        => format == DocumentFormat.Yaml ? OpenApiFormat.Yaml : OpenApiFormat.Json;

    private static async Task WriteFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    private static ExportResult Failure(
        ExportOptions options,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings,
        int exitCode)
        => new()
        {
            Success = false,
            ExitCode = exitCode,
            OutputPath = options.OutputPath,
            Output = options.Output,
            DocumentFormat = options.DocumentFormat,
            Warnings = warnings,
            Errors = errors
        };
}