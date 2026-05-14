# xUnit References

This reference supports the `xunit-test-organization` skill's guidance on test
class structure, fixtures, theories, traits, and runner compatibility.

## Official Resources

- xUnit official site: <https://xunit.net>
- xUnit shared context / fixtures: <https://xunit.net/docs/shared-context>
- xUnit getting started: <https://xunit.net/docs/getting-started/v3/getting-started>
- xUnit v3 NuGet package: <https://www.nuget.org/packages/xunit.v3>
- xUnit v2 NuGet package: <https://www.nuget.org/packages/xunit>
- xUnit Visual Studio runner: <https://www.nuget.org/packages/xunit.runner.visualstudio>

## Skill-specific interpretation

- Prefer xUnit v3 for .NET 8+ test projects
- Prefer xUnit v2 for .NET 6/7 test projects when using the older VSTest flow
- Do not mix `xunit` v2 packages with `xunit.v3`
- Use xUnit's built-in `Assert` APIs instead of FluentAssertions in this skill
- Use `[Theory]` for repeated logic across multiple inputs
- Use fixtures to control expensive or shared test setup
- In xUnit v3 async tests, forward `TestContext.Current.CancellationToken`
  to any async API that accepts a `CancellationToken` so runner-triggered
  cancellation can stop blocked operations cooperatively

## Relevant xUnit concepts

- `IClassFixture<TFixture>` — shared setup for a single test class
- `ICollectionFixture<TFixture>` — shared setup across multiple test classes
- `[Fact]` — single scenario test
- `[Theory]` with `[InlineData]` / `[MemberData]` — parameterized tests
- `[Trait]` — test categorization for filtering in CI

