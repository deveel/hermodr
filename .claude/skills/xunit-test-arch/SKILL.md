---
description: Guides the agent in structuring xUnit test projects and solutions for .NET class libraries. Use this skill when setting up a new test project, organizing the test folder layout, configuring shared build properties via Directory.Build.props, selecting xUnit and coverage packages per target framework, or establishing MSBuild conventions for test solutions.
license: MIT
metadata:
    author: Antonello Provenzano
    compatibility:
        - github-copilot
        - claude-code
        - openai-codex
    github-path: plugins/dotnet-arch/skills/xunit-test-arch
    github-ref: refs/heads/main
    github-repo: https://github.com/deveel/agents-skills
    github-tree-sha: 3015138b7747faee1885de92782da4b95e9b93a3
    version: "1.0"
name: xunit-test-arch
---
# xUnit Test Architecture

This skill establishes the structural and build-level conventions for xUnit test
solutions in .NET — covering project naming, folder layout, shared MSBuild
configuration, package selection by target framework, and coverage tooling
setup. It is the architectural counterpart to `xunit-test-organization`, which
covers test coding practices.

## When to Use

- Creating a new xUnit test project or test solution from scratch
- Reorganizing an existing `test/` or `tests/` folder to match conventions
- Configuring `Directory.Build.props` to centralize shared test packages
- Selecting the correct xUnit and coverage packages for a given target framework
- Setting up or migrating coverage collection (MTP vs VSTest)
- Adding a shared test support library (`.Testing` project)
- Multi-targeting test projects across several .NET versions

## When Not to Use

- Writing or refactoring individual test methods (use `xunit-test-organization`)
- Configuring source libraries or non-test build properties
- Setting up a single-project application that does not require a separate test layer

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Library name | Yes | The production library being tested (e.g. `MyLib`) |
| Target framework(s) | Yes | `net6.0`, `net8.0`, `net9.0`, etc. |
| Coverage tool preference | No | MTP (`Microsoft.Testing.Extensions.CodeCoverage`) or Coverlet collector; defaults to MTP for .NET 8+ |
| Shared support library needed | No | Whether a `.Testing` project is required alongside the `.XUnit` project |

## Workflow

### Step 1: Apply the naming convention

| Project type | Suffix | Example | `IsTestProject` |
|---|---|---|---|
| Executable test project | `.XUnit` | `MyLib.XUnit` | `true` (default) |
| Shared test support library | `.Testing` | `MyLib.Testing` | `false` |
| Library under test | _(none)_ | `MyLib` | _(not set)_ |

The `.XUnit` suffix signals the framework in use without permanently locking the
name — if a different framework is adopted in the future, a new
`{LibraryName}.NUnit` (or similar) project can coexist alongside it.

Shared test support libraries (fixtures, builders, fakes) that are referenced
by multiple test projects but do not contain executable tests are named with
the `.Testing` suffix and have `<IsTestProject>false</IsTestProject>` in their
`.csproj`. This prevents `Directory.Build.props` from injecting xUnit runner
packages into them.

### Step 2: Create the folder layout

```
src/
  MyLib/
    MyLib.csproj
test/
  Directory.Build.props           ← test-scope build settings
  coverlet.runsettings            ← test-scope coverage settings (VSTest only)
  MyLib.XUnit/
    MyLib.XUnit.csproj          ← IsTestProject: true (default)
    Unit/
      MyClassTests.cs
    Integration/
      MyClassIntegrationTests.cs
    Fixtures/
      MyClassFixture.cs
      MyCollectionFixture.cs
  MyLib.Testing/
    MyLib.Testing.csproj        ← IsTestProject: false
    Builders/
      OrderBuilder.cs
    Fakes/
      FakeOrderRepository.cs
```

Rules:
- Test project folder lives under `tests/` (or `test/`) at the solution root, mirroring `src/`
- Unit and integration tests are separated into `Unit/` and `Integration/` subfolders
- Never mix unit and integration tests in the same file
- Keep shared xUnit runner and coverage packages in `Directory.Build.props`
- Add package references directly to a test `.csproj` only when they are
  project-specific and not broadly needed by other test projects

### Step 3: Configure Directory.Build.props

Place `Directory.Build.props` in the `test/` folder so its scope stays within
test projects only. It injects shared test dependencies conditionally based on
`<IsTestProject>` and `<TargetFramework>`, keeping individual `.csproj` files
minimal.

```xml
<Project>

    <!-- ============================================================
         Defaults applied to projects under test/
    ============================================================ -->
    <PropertyGroup>
        <IsPackable>false</IsPackable>
        <!-- IsTestProject defaults to true; shared support libraries opt out explicitly -->
        <IsTestProject Condition="'$(IsTestProject)' == ''">true</IsTestProject>
    </PropertyGroup>

    <!-- ============================================================
         Packages injected into ALL projects under test/
         (both executable test projects and shared support libraries)
    ============================================================ -->
    <ItemGroup>
        <PackageReference Include="Bogus" Version="34.*" />
    </ItemGroup>

    <!-- ============================================================
         Packages injected ONLY into executable test projects
         (IsTestProject = true)
    ============================================================ -->

    <!-- .NET 8+ → xUnit v3 + Microsoft Testing Platform -->
    <ItemGroup Condition="'$(IsTestProject)' == 'true'
             and $([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">
        <PackageReference Include="xunit.v3" Version="1.*" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.*" />
        <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="17.*" />
        <PackageReference Include="coverlet.collector" Version="6.*">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
    </ItemGroup>

    <!-- .NET 6/7 → xUnit v2 + VSTest -->
    <ItemGroup Condition="'$(IsTestProject)' == 'true'
             and !$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">
        <PackageReference Include="xunit" Version="2.*" />
        <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
        <PackageReference Include="coverlet.collector" Version="6.*">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
    </ItemGroup>

    <!-- MTP properties for .NET 8+ test projects -->
    <PropertyGroup Condition="'$(IsTestProject)' == 'true'
                 and $([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">
        <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
        <TestingPlatformCaptureOutput>false</TestingPlatformCaptureOutput>
    </PropertyGroup>

</Project>
```

### Step 4: Create the minimal .csproj files

**Executable test project** — `Directory.Build.props` handles shared packages and MTP
properties, so the `.csproj` only needs the target framework and project reference:

```xml
<!-- test/MyLib.XUnit/MyLib.XUnit.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\MyLib\MyLib.csproj" />
    </ItemGroup>
</Project>
```

**Shared test support library** — opts out of xUnit runner injection:

```xml
<!-- test/MyLib.Testing/MyLib.Testing.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <!-- Opt out of xUnit runner injection from Directory.Build.props -->
        <IsTestProject>false</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\MyLib\MyLib.csproj" />
    </ItemGroup>
</Project>
```

The `.XUnit` project then references the `.Testing` library:

```xml
<ItemGroup>
    <ProjectReference Include="..\..\src\MyLib\MyLib.csproj" />
    <ProjectReference Include="..\MyLib.Testing\MyLib.Testing.csproj" />
</ItemGroup>
```

### Step 5: Handle multi-targeting

For solutions that target multiple frameworks, set `<TargetFrameworks>` in the
`.csproj`. The conditions in `Directory.Build.props` handle package selection
per framework automatically:

```xml
<TargetFrameworks>net6.0;net8.0;net9.0</TargetFrameworks>
```

No further changes to `Directory.Build.props` are needed.

### Step 6: Configure coverage collection

**MTP (.NET 8+):**
```bash
# Collect in Cobertura format
dotnet test --coverage --coverage-output ./coverage --coverage-output-format cobertura

# Filter and collect together
dotnet test --coverage -- --filter "Category=Unit"
```

**VSTest (.NET 6/7):**
```bash
dotnet test --collect:"XPlat Code Coverage" \
            --settings test/coverlet.runsettings \
            --results-directory ./coverage
```

Place a `coverlet.runsettings` file in the `test/` folder for VSTest exclusions.
MTP picks up the same exclusion patterns via MSBuild properties in
`Directory.Build.props` or the `--coverage-include` / `--coverage-exclude` flags.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <DataCollectionRunSettings>
        <DataCollectors>
            <DataCollector friendlyName="XPlat Code Coverage">
                <Configuration>
                    <Format>cobertura</Format>
                    <Exclude>[*.XUnit]*,[*.Testing]*,[*.Migrations]*</Exclude>
                    <ExcludeByAttribute>GeneratedCodeAttribute,ExcludeFromCodeCoverageAttribute</ExcludeByAttribute>
                    <ExcludeByFile>**/Program.cs,**/Migrations/**</ExcludeByFile>
                    <SingleHit>false</SingleHit>
                    <IncludeTestAssembly>false</IncludeTestAssembly>
                </Configuration>
            </DataCollector>
        </DataCollectors>
    </DataCollectionRunSettings>
</RunSettings>
```

## Validation

- [ ] Test projects use `.XUnit` suffix for executable projects and `.Testing` for support libraries
- [ ] `Directory.Build.props` is placed in `test/` (or `tests/`) and covers all test projects
- [ ] `IsTestProject` defaults to `true`; shared support libraries explicitly set it to `false`
- [ ] xUnit v3 + MTP packages are used for .NET 8+ projects; xUnit v2 + VSTest for .NET 6/7
- [ ] `Microsoft.NET.Test.Sdk` is NOT included in .NET 8+ projects (conflicts with MTP)
- [ ] `xunit` (v2) packages are NOT mixed with `xunit.v3`
- [ ] Individual `.csproj` files do not duplicate packages already present in `Directory.Build.props`
- [ ] Multi-targeting uses `<TargetFrameworks>` and relies on `Directory.Build.props` conditions
- [ ] Coverage tooling is configured appropriate to the target framework (MTP or VSTest/Coverlet)

## What the Agent Must Never Do

- Do not place tests directly in the solution root or `src/` folder
- Do not name test projects with the `.Tests` suffix — use `.XUnit` for executable test projects
- Do not duplicate shared test package references in individual test `.csproj`
  files — keep common dependencies in `Directory.Build.props` and add local
  `PackageReference` entries only for project-specific needs
- Do not set `<IsTestProject>false</IsTestProject>` on executable test projects —
  only shared support libraries (`.Testing`) use this opt-out
- Do not add `Microsoft.NET.Test.Sdk` to projects targeting .NET 8+ with xUnit v3 — it conflicts with MTP
- Do not add `xunit` (v2) packages alongside `xunit.v3` — they are mutually exclusive

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Naming test projects with `.Tests` suffix | Use `.XUnit` (or framework-specific suffix) so the runner is identifiable and replaceable |
| Duplicating shared packages in individual `.csproj` files | Centralise runner and coverage packages in `Directory.Build.props` |
| Adding `Microsoft.NET.Test.Sdk` to .NET 8+ projects | Remove it — MTP is incompatible with that package |
| Mixing xUnit v2 and v3 packages | Use only one major version; set conditions in `Directory.Build.props` |
| `IsTestProject` not set on support libraries | Set `<IsTestProject>false</IsTestProject>` in each `.Testing` `.csproj` |
| Coverage excluded from shared support project | Exclude `[*.XUnit]*` and `[*.Testing]*` in `coverlet.runsettings` or MTP flags |
| Missing `.Testing` project for cross-project fixtures | Add a `.Testing` project when fixtures or builders are reused by more than one test project |

