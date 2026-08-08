//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Hermodr.Tool;

using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

using Xunit;

namespace Hermodr.Tool.Tests;

/// <summary>
/// Shared helpers for resolving the bundled fixture assets that travel into
/// the test bin output directory via the
/// <c>Hermodr.AsyncApi.Exporter.Fixtures</c> project reference.
/// </summary>
internal static class FixturePaths
{
    /// <summary>
    /// Absolute path to the <c>Hermodr.AsyncApi.Exporter.Fixtures.dll</c>
    /// copied into the test output directory. This is the assembly the
    /// <c>--assembly</c> flag points at.
    /// </summary>
    public static string FixtureAssembly =>
        Path.Combine(AppContext.BaseDirectory, "Hermodr.AsyncApi.Exporter.Fixtures.dll");

    /// <summary>
    /// Absolute path to the <c>channels.json</c> shipped by the fixtures
    /// project (marked <c>CopyToOutputDirectory=PreserveNewest</c>).
    /// </summary>
    public static string FixtureChannelsFile =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "channels.json");

    /// <summary>
    /// The assembly-qualified entry specifier
    /// (<c>Type::Method</c>) used to exercise the <c>--entry</c> flag.
    /// </summary>
    public static string EntrySpecifier =>
        $"{typeof(Hermodr.AsyncApi.Exporter.Fixtures.ChannelMetadataFactory).AssemblyQualifiedName}::GetChannels";
}

public class ExportCommandTests
{
    private static async Task<(int exitCode, string output)> RunAsync(params string[] args)
    {
        var originalAnsi = AnsiConsole.Console;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var test = new TestConsole();
        using var sw = new StringWriter();
        AnsiConsole.Console = test;
        Console.SetOut(sw);
        Console.SetError(sw);
        try
        {
            var app = new CommandApp<ExportCommand>();
            app.Configure(config =>
            {
                config.SetApplicationName("hermodr-asyncapi");
                config.UseStrictParsing();
            });
            var rc = await app.RunAsync(args, CancellationToken.None);
            var output = sw + test.Output;
            return (rc, output);
        }
        finally
        {
            AnsiConsole.Console = originalAnsi;
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Help_returns_zero_and_does_not_execute()
    {
        var (rc, _) = await RunAsync("--help");
        Assert.Equal(0, rc);
    }

    [Fact]
    public async Task Missing_out_is_a_parse_error()
    {
        var (rc, _) = await RunAsync("--assembly", FixturePaths.FixtureAssembly);
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public async Task Missing_assembly_returns_1()
    {
        var (rc, output) = await RunAsync("--out", Path.Join(Path.GetTempPath(), "x.json"));
        Assert.Equal(1, rc);
        Assert.Contains("at least one --assembly", output);
    }

    [Fact]
    public async Task Unknown_format_is_a_parse_error()
    {
        var (rc, _) = await RunAsync("--out", Path.Join(Path.GetTempPath(), "x.json"), "--format", "bogus");
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public async Task Unknown_output_is_a_parse_error()
    {
        var (rc, _) = await RunAsync("--out", Path.Join(Path.GetTempPath(), "x.json"), "--output", "bar");
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public async Task Unknown_option_is_a_parse_error()
    {
        var (rc, _) = await RunAsync("--out", Path.Join(Path.GetTempPath(), "x.json"), "--bogus");
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public async Task Happy_path_writes_asyncapi_json()
    {
        var outPath = Path.Join(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.json");
        try
        {
            var (rc, output) = await RunAsync(
                "--assembly", FixturePaths.FixtureAssembly,
                "--channels-file", FixturePaths.FixtureChannelsFile,
                "--output", "asyncapi",
                "--format", "json",
                "--title", "Test Events",
                "--version", "2.0",
                "--out", outPath);

            Assert.Equal(0, rc);
            Assert.Contains("wrote", output);
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
    public async Task Happy_path_writes_openapi_yaml()
    {
        var outPath = Path.Join(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.yaml");
        try
        {
            var (rc, output) = await RunAsync(
                "--assembly", FixturePaths.FixtureAssembly,
                "--channels-file", FixturePaths.FixtureChannelsFile,
                "--output", "openapi",
                "--format", "yaml",
                "--out", outPath);

            Assert.Equal(0, rc);
            Assert.Contains("wrote", output);
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
    public async Task Entry_specifier_resolves_channels_via_factory()
    {
        var outPath = Path.Join(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.json");
        try
        {
            var (rc, output) = await RunAsync(
                "--assembly", FixturePaths.FixtureAssembly,
                "--entry", FixturePaths.EntrySpecifier,
                "--out", outPath);

            Assert.Equal(0, rc);
            Assert.Contains("wrote", output);
            Assert.True(File.Exists(outPath));
            var content = await File.ReadAllTextAsync(outPath);
            Assert.Contains("\"asyncapi\"", content);
            // The factory declares a webhook channel for order-confirmed;
            // its server URL should appear in the document.
            Assert.Contains("example.com", content);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task Equals_syntax_is_supported()
    {
        var outPath = Path.Join(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.json");
        try
        {
            var (rc, _) = await RunAsync(
                $"--assembly={FixturePaths.FixtureAssembly}",
                $"--out={outPath}",
                "--format=json");

            Assert.Equal(0, rc);
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task Repeated_assemblies_accumulate()
    {
        var outPath = Path.Join(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.json");
        try
        {
            var (rc, _) = await RunAsync(
                "--assembly", FixturePaths.FixtureAssembly,
                "--assembly", FixturePaths.FixtureAssembly,
                "--out", outPath);

            Assert.Equal(0, rc);
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task Defaults_asyncapi_json_when_unspecified()
    {
        var outPath = Path.Join(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.json");
        try
        {
            var (rc, output) = await RunAsync(
                "--assembly", FixturePaths.FixtureAssembly,
                "--channels-file", FixturePaths.FixtureChannelsFile,
                "--out", outPath);

            Assert.Equal(0, rc);
            Assert.Contains("(AsyncApi, Json)", output);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }
}