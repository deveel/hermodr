//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Hermodr;

using Xunit;

namespace Hermodr.Tool.Tests;

/// <summary>
/// End-to-end orchestration tests against the console-agnostic
/// <see cref="ExportService"/>. No Spectre.Console involvement — these verify
/// the library returns the expected <see cref="ExportResult"/> and writes the
/// expected file content.
/// </summary>
public class ExportServiceTests
{
    private static string FixtureAssembly =>
        Path.Combine(AppContext.BaseDirectory, "Hermodr.AsyncApi.Exporter.Fixtures.dll");

    private static string FixtureChannelsFile =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "channels.json");

    private static string NewOutputPath(string ext) =>
        Path.Join(Path.GetTempPath(), $"hermodr-svc-{Guid.NewGuid():N}.{ext}");

    [Fact]
    public async Task No_assemblies_yields_validation_failure()
    {
        var result = await ExportService.ExportAsync(new ExportOptions
        {
            Assemblies = Array.Empty<string>(),
            OutputPath = NewOutputPath("json")
        });

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("at least one --assembly"));
    }

    [Fact]
    public async Task AsyncApi_json_happy_path()
    {
        var outPath = NewOutputPath("json");
        try
        {
            var result = await ExportService.ExportAsync(new ExportOptions
            {
                Assemblies = new[] { FixtureAssembly },
                ChannelsFile = FixtureChannelsFile,
                Output = OutputFormat.AsyncApi,
                DocumentFormat = DocumentFormat.Json,
                Title = "Test Events",
                Version = "2.0",
                OutputPath = outPath
            });

            Assert.True(result.Success, string.Join("\n", result.Errors));
            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(outPath));
            var content = await File.ReadAllTextAsync(outPath);
            Assert.Contains("\"asyncapi\"", content);
            Assert.Contains("Test Events", content);
            Assert.Contains("2.0", content);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task OpenApi_yaml_happy_path()
    {
        var outPath = NewOutputPath("yaml");
        try
        {
            var result = await ExportService.ExportAsync(new ExportOptions
            {
                Assemblies = new[] { FixtureAssembly },
                ChannelsFile = FixtureChannelsFile,
                Output = OutputFormat.OpenApi,
                DocumentFormat = DocumentFormat.Yaml,
                OutputPath = outPath
            });

            Assert.True(result.Success, string.Join("\n", result.Errors));
            Assert.True(File.Exists(outPath));
            var content = await File.ReadAllTextAsync(outPath);
            Assert.Contains("openapi:", content);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task Entry_resolver_supplies_channels()
    {
        var outPath = NewOutputPath("json");
        try
        {
            var entry = $"{typeof(Hermodr.AsyncApi.Exporter.Fixtures.ChannelMetadataFactory).AssemblyQualifiedName}::GetChannels";
            var result = await ExportService.ExportAsync(new ExportOptions
            {
                Assemblies = new[] { FixtureAssembly },
                Entry = entry,
                OutputPath = outPath
            });

            Assert.True(result.Success, string.Join("\n", result.Errors));
            Assert.True(File.Exists(outPath));
            var content = await File.ReadAllTextAsync(outPath);
            Assert.Contains("example.com", content);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task Missing_channels_file_records_warning_and_still_exports()
    {
        var outPath = NewOutputPath("json");
        try
        {
            var result = await ExportService.ExportAsync(new ExportOptions
            {
                Assemblies = new[] { FixtureAssembly },
                ChannelsFile = Path.Join(Path.GetTempPath(), $"no-such-{Guid.NewGuid():N}.json"),
                OutputPath = outPath
            });

            Assert.True(result.Success, string.Join("\n", result.Errors));
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("channels-file"));
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task Unloadable_assembly_records_warning_but_continues()
    {
        var outPath = NewOutputPath("json");
        try
        {
            var result = await ExportService.ExportAsync(new ExportOptions
            {
                Assemblies = new[] { "/tmp/definitely-not-here-xyz.dll", FixtureAssembly },
                OutputPath = outPath
            });

            Assert.True(result.Success, string.Join("\n", result.Errors));
            Assert.Contains(result.Warnings, w => w.Contains("could not load assembly"));
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task Output_directory_is_created_if_missing()
    {
        var dir = Path.Join(Path.GetTempPath(), $"hermodr-outdir-{Guid.NewGuid():N}", "nested");
        var outPath = Path.Join(dir, "asyncapi.json");
        try
        {
            var result = await ExportService.ExportAsync(new ExportOptions
            {
                Assemblies = new[] { FixtureAssembly },
                OutputPath = outPath
            });

            Assert.True(result.Success, string.Join("\n", result.Errors));
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(Path.GetDirectoryName(dir)!, true);
        }
    }
}