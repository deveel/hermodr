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

public class ExportCommandTests
{
    private static string SampleAssemblyPath
    {
        get
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var probe = Path.Combine(dir, "asyncapi-export.dll");
            if (File.Exists(probe))
                return probe;

            var baseDir = AppContext.BaseDirectory;
            for (var d = new DirectoryInfo(baseDir); d != null; d = d.Parent)
            {
                var candidate = Path.Combine(d.FullName, "samples", "asyncapi-export", "bin");
                if (Directory.Exists(candidate))
                {
                    var dll = Directory.GetFiles(candidate, "asyncapi-export.dll", SearchOption.AllDirectories)
                        .FirstOrDefault(p => p.Contains("net10.0"));
                    if (dll != null)
                        return dll;
                }
            }

            throw new FileNotFoundException(
                "Could not locate the sample 'asyncapi-export.dll' (net10.0). " +
                "Ensure the sample project is built before running the tests.", probe);
        }
    }

    private static string SampleChannelsFile
    {
        get
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var probe = Path.Combine(dir, "channels.json");
            if (File.Exists(probe))
                return probe;

            var baseDir = AppContext.BaseDirectory;
            for (var d = new DirectoryInfo(baseDir); d != null; d = d.Parent)
            {
                var candidate = Path.Combine(d.FullName, "samples", "asyncapi-export", "channels.json");
                if (File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException("Could not locate the sample 'channels.json'.", "channels.json");
        }
    }

    private static async Task<(int exitCode, string output)> RunAsync(params string[] args)
    {
        var original = AnsiConsole.Console;
        var test = new TestConsole();
        AnsiConsole.Console = test;
        try
        {
            var app = new CommandApp<ExportCommand>();
            app.Configure(config =>
            {
                config.SetApplicationName("hermodr-asyncapi");
                config.UseStrictParsing();
            });
            var rc = await app.RunAsync(args, CancellationToken.None);
            return (rc, test.Output);
        }
        finally
        {
            AnsiConsole.Console = original;
        }
    }

    [Fact]
    public async Task Help_lists_all_options()
    {
        var (rc, output) = await RunAsync("--help");
        Assert.Equal(0, rc);
        Assert.Contains("--assembly", output);
        Assert.Contains("--output", output);
        Assert.Contains("--format", output);
        Assert.Contains("--title", output);
        Assert.Contains("--version", output);
        Assert.Contains("--out", output);
        Assert.Contains("--channels-file", output);
        Assert.Contains("--entry", output);
    }

    [Fact]
    public async Task Missing_out_is_a_parse_error()
    {
        var (rc, output) = await RunAsync("--assembly", "/tmp/nope.dll");
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public async Task Missing_assembly_returns_1()
    {
        var (rc, output) = await RunAsync("--out", Path.Combine(Path.GetTempPath(), "x.json"));
        Assert.Equal(1, rc);
        Assert.Contains("at least one --assembly", output);
    }

    [Fact]
    public async Task Unknown_format_is_a_parse_error()
    {
        var (rc, _) = await RunAsync("--out", Path.Combine(Path.GetTempPath(), "x.json"), "--format", "bogus");
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public async Task Unknown_output_is_a_parse_error()
    {
        var (rc, _) = await RunAsync("--out", Path.Combine(Path.GetTempPath(), "x.json"), "--output", "bar");
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public async Task Unknown_option_is_a_parse_error()
    {
        var (rc, _) = await RunAsync("--out", Path.Combine(Path.GetTempPath(), "x.json"), "--bogus");
        Assert.NotEqual(0, rc);
    }

    [Fact]
    public async Task Happy_path_writes_asyncapi_json()
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.json");
        try
        {
            var (rc, output) = await RunAsync(
                "--assembly", SampleAssemblyPath,
                "--channels-file", SampleChannelsFile,
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
        var outPath = Path.Combine(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.yaml");
        try
        {
            var (rc, output) = await RunAsync(
                "--assembly", SampleAssemblyPath,
                "--channels-file", SampleChannelsFile,
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
    public async Task Equals_syntax_is_supported()
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.json");
        try
        {
            var (rc, _) = await RunAsync(
                $"--assembly={SampleAssemblyPath}",
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
        var outPath = Path.Combine(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.json");
        try
        {
            var (rc, _) = await RunAsync(
                "--assembly", SampleAssemblyPath,
                "--assembly", SampleAssemblyPath,
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
        var outPath = Path.Combine(Path.GetTempPath(), $"hermodr-test-{Guid.NewGuid():N}.json");
        try
        {
            var (rc, output) = await RunAsync(
                "--assembly", SampleAssemblyPath,
                "--channels-file", SampleChannelsFile,
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