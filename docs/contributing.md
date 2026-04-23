# Contributing to Deveel Events

Thank you for your interest in contributing!  We welcome bug reports, feature requests, documentation improvements, and pull requests.

## Code of Conduct

Please be respectful and constructive in all interactions.  We follow the [Contributor Covenant](https://www.contributor-covenant.org/) code of conduct.

## Getting Started

1. **Fork** the repository on GitHub.
2. **Clone** your fork:
   ```bash
   git clone https://github.com/<your-username>/deveel.events.git
   cd deveel.events
   ```
3. **Create a feature branch**:
   ```bash
   git checkout -b feature/my-new-feature
   ```
4. Make your changes and add or update tests as appropriate.
5. **Build and test** before submitting:
   ```bash
   dotnet build
   dotnet test
   ```
6. **Commit** with a clear, descriptive message.
7. **Push** to your fork and open a **Pull Request** against the `main` branch.

## Project Structure

```
.
├── src/          # Production source code (one folder per NuGet package)
├── test/         # Test projects (one per source package, using xUnit)
├── benchmarks/   # BenchmarkDotNet projects
├── docs/         # Documentation (this folder)
└── .github/      # GitHub Actions workflows
```

## Building

```bash
dotnet restore
dotnet build -c Release
```

## Running Tests

```bash
# Run all tests
dotnet test -c Release

# Skip RabbitMQ tests (useful when no broker is running locally)
dotnet test -c Release -- --filter-not-trait "Channel=RabbitMQ"
```

## Coding Guidelines

- Follow the existing code style (C# 12, nullable reference types enabled).
- All public types and members should have XML documentation comments.
- Add `[Fact]` or `[Theory]` tests for any new behaviour.
- Do not include breaking changes in minor or patch releases without discussion.

## Reporting Issues

Open a [GitHub Issue](https://github.com/deveel/deveel.events/issues) and include:

- A clear title and description.
- The package version affected.
- A minimal repro (code snippet or test case).
- Observed vs. expected behaviour.

## Feature Requests

Open a GitHub Issue labelled `enhancement`.  Describe the use-case and proposed API before submitting a pull request for large changes.

## License

By contributing you agree that your contributions will be licensed under the [MIT License](https://github.com/deveel/deveel.events/blob/main/LICENSE).

