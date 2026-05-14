# xUnit Test Organization References

This folder contains focused reference material that supports the
`xunit-test-organization` skill.

Use these files when an agent needs narrower context than the main `SKILL.md`
provides, such as package selection, fixture design, async cancellation token
flow in xUnit v3 tests, coverage setup, or test project layout decisions.

## Index

- [xunit.md](./xunit.md) — xUnit framework references, fixtures, theories, traits, and async test cancellation guidance
- [dotnet-testing-platform.md](./dotnet-testing-platform.md) — test runner and platform guidance for .NET 6/7 vs .NET 8+, including cooperative cancellation notes for xUnit v3
- [coverage.md](./coverage.md) — code coverage collection and report generation references
- [bogus.md](./bogus.md) — randomized test data guidance and package references
- [msbuild.md](./msbuild.md) — `tests/Directory.Build.props` and test-scope package centralization

## Intent

These references are intentionally supplemental:

- `SKILL.md` remains the authoritative instruction set for the agent
- Files in this folder provide deeper background and external source links
- Topic files are organized to mirror the main skill's guidance areas

