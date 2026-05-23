using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BenchmarkDotNet.Validators;

using HermodrBenchmarks;

const string x64CliPath = @"C:\Program Files\dotnet\dotnet.exe";
const string x86CliPath = @"C:\Program Files (x86)\dotnet\dotnet.exe";

var dotnet60Cli32bit = NetCoreAppSettings
    .NetCoreApp60
    .WithCustomDotNetCliPath(x86CliPath, ".NET 6.0 x86 cli");

var dotnet60Cli64bit = NetCoreAppSettings
    .NetCoreApp60
    .WithCustomDotNetCliPath(x64CliPath, ".NET 6.0 x64 cli");

var dotnet70Cli32bit = NetCoreAppSettings
    .NetCoreApp70
    .WithCustomDotNetCliPath(x86CliPath, ".NET 7.0 x86 cli");

var dotnet70Cli64bit = NetCoreAppSettings
    .NetCoreApp70
    .WithCustomDotNetCliPath(x64CliPath, ".NET 7.0 x64 cli");

var dotnet80Cli32bit = NetCoreAppSettings
    .NetCoreApp80
    .WithCustomDotNetCliPath(x86CliPath, ".NET 8.0 x86 cli");

var dotnet80Cli64bit = NetCoreAppSettings
    .NetCoreApp80
    .WithCustomDotNetCliPath(x64CliPath, ".NET 8.0 x64 cli");

BenchmarkRunner.Run<PublishBenchmarks>(ManualConfig
    .Create(DefaultConfig.Instance)
    .AddJob(Job.Default.WithPlatform(Platform.X86).WithJit(Jit.RyuJit).WithToolchain(CsProjCoreToolchain.From(dotnet60Cli32bit)))
    .AddJob(Job.Default.WithPlatform(Platform.X64).WithJit(Jit.RyuJit).WithToolchain(CsProjCoreToolchain.From(dotnet60Cli64bit)))
    .AddJob(Job.Default.WithPlatform(Platform.X86).WithJit(Jit.RyuJit).WithToolchain(CsProjCoreToolchain.From(dotnet70Cli32bit)))
    .AddJob(Job.Default.WithPlatform(Platform.X64).WithJit(Jit.RyuJit).WithToolchain(CsProjCoreToolchain.From(dotnet70Cli64bit)))
    .AddJob(Job.Default.WithPlatform(Platform.X86).WithJit(Jit.RyuJit).WithToolchain(CsProjCoreToolchain.From(dotnet80Cli32bit)))
    .AddJob(Job.Default.WithPlatform(Platform.X64).WithJit(Jit.RyuJit).WithToolchain(CsProjCoreToolchain.From(dotnet80Cli64bit)))
    .AddValidator(ExecutionValidator.FailOnError)
    .AddDiagnoser(MemoryDiagnoser.Default));
    // .AddDiagnoser(PerfCollectProfiler.Default));

// BenchmarkRunner.Run<PublishBenchmarks>();