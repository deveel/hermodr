---
description: Guides the agent in structuring a custom .NET framework composed of reusable libraries for community consumption. Use this skill when asked to design or reorganize solution layout, package boundaries, versioning, and distribution for multi-library frameworks.
license: MIT
metadata:
    author: Antonello Provenzano
    compatibility:
        - github-copilot
        - claude-code
        - openai-codex
    github-path: plugins/dotnet-arch/skills/custom-framework-arch
    github-ref: refs/heads/main
    github-repo: https://github.com/deveel/agents-skills
    github-tree-sha: e8411844634b279dff9fae63999e600922ed6ded
    version: "1.0"
name: custom-framework-arch
---
# Custom Framework Organization

## Purpose

This skill helps agents create a maintainable, scalable structure for a custom .NET framework that ships multiple reusable libraries to the wider .NET community through NuGet and clear public APIs.

## When to Use

- Designing a new multi-library .NET framework from scratch
- Refactoring an existing repository into clear package and project boundaries
- Defining conventions for shared abstractions, implementations, and extensions
- Preparing a framework for long-term versioning, documentation, and public adoption

## When Not to Use

- Building a single application that is not meant to be reused as a framework
- Organizing test-only repositories without distributable libraries
- Migrating runtime infrastructure unrelated to .NET package architecture

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Framework name | Yes | Root framework name used in solution, namespaces, and package IDs |
| Main library naming preference | No | Preferred main package name: `{Framework}.Core` or `{Framework}`; ask when not specified |
| Target TFMs | Yes | Supported target frameworks (for example `net8.0`, `net9.0`) |
| Library domains | Yes | Functional areas to split into packages (for example `Core`, `Abstractions`, `Http`) |
| Distribution model | Yes | NuGet packaging strategy (public feed, prerelease, stable cadence) |
| Compatibility policy | No | Backward-compatibility and deprecation policy |

## Workflow

1. Define package taxonomy and boundaries.
2. Standardize repository and solution layout.
3. Define API layering and dependency rules.
4. Establish shared build and packaging configuration.
5. Build quality gates for community-ready delivery.
6. Document consumption patterns.
7. Run documentation maintenance routines after every change.

### Step 1: Define package taxonomy and boundaries

Split the framework into focused packages with explicit responsibilities:

- `{Framework}.Abstractions` (optional): interfaces, contracts, and shared primitives when split from the main package
- Main package: `{Framework}.Core` (preferred) or `{Framework}` when requested by the developer
- `{Framework}.{Feature}`: feature-specific modules (for example `Caching`, `Serialization`, `Hosting`)
- `{Framework}.Extensions.*`: optional adapters and integrations

Rules:

- Keep `Abstractions` dependency-light and stable
- Prevent circular references across packages
- Depend inward on abstractions, not concrete feature packages
- If no naming preference is provided, ask whether the main package should be `{Framework}.Core` or `{Framework}` before scaffolding

### Step 2: Standardize repository and solution layout

Use a consistent layout that separates source, tests, examples, and docs:

```text
src/
  {Framework}.Abstractions/        (optional)
  {Framework}.Core/ or {Framework}/
  {Framework}.{FeatureA}/
  {Framework}.{FeatureB}/
test/
  {Framework}.Abstractions.XUnit/   (optional)
  {Framework}.Core.XUnit/ or {Framework}.XUnit/
  {Framework}.{FeatureA}.XUnit/
samples/
  BasicUsage/
  AdvancedUsage/
docs/

```

Conventions:

- One project per package
- One test project per source project
- Keep sample apps as consumers, never as internal implementation hosts

### Step 3: Define API layering and dependency rules

Apply strict layering to keep public contracts stable:

- Public API surfaces live in `Abstractions` or dedicated public packages
- Internal helper types should be `internal` and hidden from package consumers
- Extension methods and DI registration belong in `Extensions` packages

Use analyzers or architecture tests to block illegal dependencies (for example feature-to-feature leakage).

### Step 4: Establish shared build and packaging configuration

Centralize common configuration in `Directory.Build.props` and `Directory.Packages.props`:

- Common compiler settings (nullable, analyzers, warnings as errors)
- Shared package versions via central package management
- Deterministic builds and SourceLink configuration
- Package metadata (title, license, authors, copyright, repository URL, tags, readme)

Package metadata rules for `.csproj` files:

- Set `Title` to a concise feature-focused title (for example, `Deveel Caching`)
- Include explicit license metadata using `PackageLicenseExpression` for SPDX licenses or `PackageLicenseFile` when a custom license file is required
- Set `Authors` to the maintainer, team, or organization responsible for the package
- Set `Copyright` to the applicable copyright notice for the package and source distribution
- Set `PackageTags` to feature-aligned tags (for example, `caching;distributed-cache;dotnet`)
- Keep tags specific to package behavior, not generic organization tags only

For open-source packages, include these properties:

- `RepositoryUrl` and `RepositoryType` (`git`)
- `PublishRepositoryUrl=true`
- `IncludeSymbols=true` and `SymbolPackageFormat=snupkg`
- Source linking enabled via SourceLink package for the Git provider

Example baseline:

```xml
<PropertyGroup>
  <Title>Contoso Caching</Title>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <Authors>Contoso</Authors>
  <Copyright>Copyright © Contoso</Copyright>
  <PackageTags>caching;memory-cache;distributed-cache;dotnet</PackageTags>
  <RepositoryUrl>https://github.com/contoso/framework</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
</PropertyGroup>
```

When using an open-source license, require SourceLink so published packages can map binaries back to source revisions.

Package naming and versioning rules:

- Package IDs follow `FrameworkName.Component`
- Use SemVer with prerelease channels (`-alpha`, `-rc`) when needed
- Version related packages together when cross-package compatibility is strict

### Step 5: Build quality gates for community-ready delivery

For each package, require:

- Unit tests and integration tests with coverage thresholds
- API compatibility checks before release
- Changelog updates and migration notes for breaking changes
- CI matrix validation across supported target frameworks

Release guidelines:

- Publish only packable projects
- Keep deprecated APIs with `[Obsolete]` before removal when feasible
- Ensure docs and samples are updated in the same release

### Step 6: Document consumption patterns

Provide practical documentation for adopters:

- Quick start for minimal setup
- Dependency graph and package selection guidance
- Version compatibility table
- Upgrade guides between minor and major versions

Treat docs as part of the framework contract, not optional extras.

### Step 7: Run documentation maintenance routines after every change

After each change that affects architecture, package boundaries, public APIs, build, or release flow, run a documentation update routine:

- Update impacted docs in `docs/` (for example, architecture overview, package selection guide, versioning policy)
- Update package-level README files when package metadata, APIs, or setup change
- Update dependency graph or package matrix when references change
- Add migration notes for behavioral or breaking changes

Use this routine as a release gate: no feature or refactor is complete until documentation is synchronized.

## Validation

- [ ] Package boundaries are explicit and avoid circular dependencies
- [ ] Main library naming preference (`{Framework}.Core` or `{Framework}`) is explicit or was confirmed with the developer
- [ ] Repository layout follows `src/`, `test/`, `samples/`, and `docs/` conventions
- [ ] Public API is intentionally defined and isolated from internal implementation
- [ ] Build and package settings are centralized and consistent across projects
- [ ] Package metadata includes title, license, authors, copyright, feature-aligned tags, and (for open source) symbols, repository fields, and SourceLink
- [ ] Release process includes tests, compatibility checks, and documentation updates
- [ ] Documentation maintenance routine has been executed for all changed areas

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Monolithic package with unrelated concerns | Split into cohesive packages mapped to clear domains |
| Assumed `.Core` or base package name without confirming preference | Ask for naming preference when not provided and apply it consistently |
| Forcing a separate `.Abstractions` package in every solution | Keep `.Abstractions` optional and merge contracts into the main package when appropriate |
| Abstractions package depends on concrete libraries | Keep abstractions at the bottom of the dependency graph |
| Inconsistent package versions across related modules | Adopt a clear versioning strategy and automate checks in CI |
| Generic or missing package tags and title | Define feature-specific `Title` and `PackageTags` for each package |
| Missing legal or ownership metadata in published packages | Add license metadata, `Authors`, and `Copyright` in shared package properties or per-package overrides |
| Open-source package published without symbols or repository metadata | Add symbols (`snupkg`), repository fields, and SourceLink before publishing |
| Samples tightly coupled to internal code | Keep samples as normal external consumers of public packages |
| Breaking API changes without migration path | Mark deprecated APIs first and publish upgrade guidance |
| Code changes shipped without doc updates | Run a mandatory post-change documentation routine before merge |







