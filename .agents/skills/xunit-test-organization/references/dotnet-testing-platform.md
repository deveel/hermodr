# .NET Testing Platform References

This reference supports the skill's runner and package guidance for different
.NET target frameworks.

## Official Resources

- `dotnet test` CLI: <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test>
- Microsoft Testing Platform introduction: <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro>
- Microsoft.NET.Test.Sdk NuGet package: <https://www.nuget.org/packages/Microsoft.NET.Test.Sdk>
- Microsoft.Testing.Extensions.CodeCoverage NuGet package: <https://www.nuget.org/packages/Microsoft.Testing.Extensions.CodeCoverage>

## Skill-specific interpretation

- For .NET 8+ test projects, prefer Microsoft Testing Platform with xUnit v3
- For .NET 6/7 test projects, use the VSTest-compatible setup with `Microsoft.NET.Test.Sdk`
- Do not add `Microsoft.NET.Test.Sdk` to .NET 8+ projects in this skill's recommended layout
- Keep shared test package wiring centralized in `test/Directory.Build.props`; add
  package references in individual test projects only when they are
  project-specific
- In xUnit v3 async tests on .NET 8+, pass `TestContext.Current.CancellationToken`
  into async APIs that accept a `CancellationToken` so Microsoft Testing Platform
  can cancel long-running or blocked test operations cooperatively

## Quick comparison

| Target framework | Preferred stack in this skill |
|---|---|
| .NET 8+ | `xunit.v3` + `xunit.runner.visualstudio` + `Microsoft.Testing.Extensions.CodeCoverage` |
| .NET 6/7 | `xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` |

