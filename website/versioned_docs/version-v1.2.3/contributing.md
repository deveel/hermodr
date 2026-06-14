# Contributing to Deveel Events

Thank you for your interest in contributing!  We welcome bug reports, feature requests, documentation improvements, and pull requests.

## Code of Conduct

Please be respectful and constructive in all interactions.  We follow the [Contributor Covenant](https://www.contributor-covenant.org/) code of conduct.

## Development Workflow

### 1. Open an Issue First

**No development should begin without a corresponding GitHub Issue.**  Before writing any code (feature or bug fix), open an issue at [github.com/deveel/deveel.events/issues](https://github.com/deveel/deveel.events/issues) and describe:

- What you want to build or fix.
- Why it is needed (use-case or observed behaviour).
- Any proposed API surface or design notes.

This ensures work is visible, avoids duplication, and allows maintainers to give early feedback.

### 2. Create a Branch via GitHub

Once an issue exists and has been acknowledged, create a branch **from the GitHub website** (issue page → "Create a branch") so that the branch name is generated consistently.  The naming convention differs by issue type:

| Issue type | Branch prefix | Example |
|---|---|---|
| Feature / enhancement | *(none — keep the auto-generated name)* | `123-add-dead-letter-channel` |
| Bug fix | `bugfix/` | `bugfix/123-fix-null-reference` |

For **bug fix** issues, GitHub will auto-generate a plain `123-slug` name — **edit it** in the "Create a branch" dialog to prepend `bugfix/` before clicking *Create branch*.  This prefix is what GitVersion uses to detect the correct patch-version increment.

> **Why the distinction?**  GitVersion reads the branch name to determine the pre-release tag and the version increment strategy.  Feature branches produce `alpha` pre-releases and bump the minor version; `bugfix/` branches produce `bugfix` pre-releases and bump the patch version.

If you must create the branch locally, follow the same naming convention:

```bash
# Feature
git fetch origin
git checkout -b 123-add-dead-letter-channel origin/main

# Bug fix
git fetch origin
git checkout -b bugfix/123-fix-null-reference origin/main
```

### 3. Develop in the Feature Branch

- All work — commits, pushes, experiments — lives in the feature branch.  The `main` branch is **never committed to directly**.
- Features do **not** need to be implemented in roadmap order.  Any planned roadmap item can be picked up and worked on in its own feature branch at any time, regardless of what milestone it targets.
- Keep the branch focused on a single issue.  If related work surfaces, open a new issue and a separate branch.

### 4. Merging into `main`

Feature branches are **not** merged into `main` immediately upon completion.  A branch is eligible for merge only when the `main` branch lifecycle has reached (or is actively preparing) the **major version** in which the feature is planned for delivery (see the [Roadmap](../ROADMAP.md)).

| Planned milestone | Merge into `main` when… |
|-------------------|--------------------------|
| `v1.x.0`          | `main` is on the `v1` release train |
| `v2.x.0`          | `main` is on the `v2` release train |

This keeps `main` releasable at all times and prevents half-finished features from blocking a release.  Every merge to `main` automatically produces a pre-release package; a git tag produces a stable release.  See the [Versioning Strategy](versioning.md) for the full details.

### 5. Pull Request

When a branch is ready to be merged:

1. **Fork** the repository on GitHub (external contributors).
2. **Clone** your fork:
   ```bash
   git clone https://github.com/<your-username>/deveel.events.git
   cd deveel.events
   ```
3. **Build and test** before submitting:
   ```bash
   dotnet build
   dotnet test
   ```
4. **Commit** with a clear, descriptive message that references the issue (`Closes #123`).
5. **Title your PR** using [Conventional Commits](https://www.conventionalcommits.org/) format — the PR title becomes the squash-merge commit message on `main` and **drives the automatic version bump**:

   | PR title prefix | Version bump | Example |
   |---|---|---|
   | `fix:` or `fix(<scope>):` | Patch — `x.y.Z` | `fix(publisher): resolve null reference on empty payload` |
   | `feat:` or `feat(<scope>):` | Minor — `x.Y.0` | `feat(schema): add AsyncAPI export support` |
   | `feat!:` or `BREAKING CHANGE:` | Major — `X.0.0` | `feat!: redesign IEventPublisher interface` |
   | `docs:`, `chore:`, `ci:`, `test:` | No bump | `docs: update contributing guide` |

6. **Push** to your fork (or the feature branch on the upstream repo for collaborators) and open a **Pull Request** against `main`, referencing the original issue.

## Bug Fixes

### Priority

**Bugs take priority over features.**  When an active bug fix branch exists, it should be reviewed and merged before any pending feature branch.  Contributors are encouraged to pause feature work and help validate or review open bug fixes.

### Lifecycle

1. **Open a bug issue** — label it `bug` and describe the defect clearly (see [Reporting Issues](#reporting-issues)).
2. **Create a fix branch** from the GitHub issue page.  Edit the auto-generated branch name to add the `bugfix/` prefix (e.g. `bugfix/123-fix-null-reference`).  This prefix is required for GitVersion to detect the patch increment.
3. **Fix, test, and open a PR** — the PR must include a regression test that reproduces the defect and passes after the fix.  Title the PR with `fix(<scope>): <description>` so the squash-merge commit triggers a patch version bump automatically.
4. **Merge as soon as possible** — unlike feature branches, a bug-fix branch is merged into `main` immediately once the PR is approved, regardless of the current release train or roadmap milestone.

### Versioning & Release

| Event | Version bump |
|-------|-------------|
| Bug fix merged into `main` | **Patch** — `x.y.Z` |

A new patch release is published **immediately** after a bug-fix merge.  There is no batching of bug fixes into a future scheduled release.  See the [Versioning Strategy](versioning.md) for the full release lifecycle.

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

> **Remember:** an open issue is a prerequisite for any development.  Please do not open a pull request without a linked issue.

## Feature Requests

Open a GitHub Issue labelled `enhancement`.  Describe the use-case and proposed API before submitting a pull request for large changes.  Once the issue is created and acknowledged, follow the [Development Workflow](#development-workflow) above to start implementation.

## License

By contributing you agree that your contributions will be licensed under the [MIT License](https://github.com/deveel/deveel.events/blob/main/LICENSE).

