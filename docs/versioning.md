# Versioning Strategy

This document describes how Deveel Events is versioned, how version numbers are calculated automatically, and how releases are produced.

---

## Semantic Versioning

Deveel Events follows [Semantic Versioning 2.0.0](https://semver.org/):

```
MAJOR.MINOR.PATCH[-pre-release]
```

| Segment | Changes when… |
|---------|--------------|
| `MAJOR` | A maintainer explicitly tags a new major baseline (e.g. `v2.0.0`) |
| `MINOR` | A maintainer explicitly tags a new minor baseline (e.g. `v1.3.0`) |
| `PATCH` | Every merge into `main` (automatic) |

---

## Automated Version Calculation — GitVersion

Version numbers are computed automatically by [GitVersion](https://gitversion.net/) from the git history. No manual version editing is required. The two inputs that drive the calculation are:

1. **The last git tag** on `main` — establishes the baseline version (major and minor).
2. **The commit count since that tag** — determines the patch number for pre-release builds.

> **Note:** Commit message prefixes (`feat:`, `fix:`, etc.) do **not** influence the version number. They are used for readability and changelog generation only.

### Conventional Commits — PR title format

PR titles **should** follow the [Conventional Commits](https://www.conventionalcommits.org/) specification for changelog clarity, but they have no effect on the computed version.

| PR title prefix | Purpose |
|---|---|
| `feat:` / `feat(<scope>):` | Documents a new feature in the changelog |
| `fix:` / `fix(<scope>):` | Documents a bug fix |
| `feat!:` / `BREAKING CHANGE:` | Documents a breaking change |
| `docs:`, `chore:`, `ci:`, `test:`, `build:` | Maintenance; no changelog entry needed |

> **Tip:** The `<scope>` is optional and refers to the package or subsystem affected, e.g. `publisher`, `schema`, `outbox`.

---

## Branch Version Labels

While work is in progress on a branch, GitVersion produces a pre-release version to identify CI builds. These are **not** published as public stable releases.

| Branch type | Naming convention | Example branch | Version produced |
|---|---|---|---|
| `main` | — | `main` | `1.2.3-pre.5` |
| Feature / enhancement | `<issue-number>-<slug>` | `123-add-dead-letter-channel` | `1.2.3-alpha.2` |
| Bug fix | `bugfix/<issue-number>-<slug>` | `bugfix/99-fix-null-reference` | `1.2.3-bugfix.1` |
| Pull request | auto | PR #42 | `1.2.3-pr.1` |

The number at the end of the pre-release tag is the **commit count since the last stable tag** on that branch.

---

## Release Lifecycle

```
     git tag v1.2.0
          │
          ▼
    ┌─────────────┐    merge (any)    ┌──────────────┐    merge (any)    ┌──────────────┐
    │  main 1.2.0 │ ───────────────► │ 1.2.1-pre.1  │ ───────────────► │ 1.2.1-pre.2  │
    └─────────────┘                  └──────┬───────┘                   └──────┬───────┘
                                            │ CI → GitHub Packages              │
                                            │                       git tag v1.2.1
                                            │                                   ▼
                                            │                           ┌──────────────┐
                                            │                           │  1.2.1 ✅    │ ← NuGet.org stable
                                            │                           └──────────────┘
                                            │
                          when milestone features are ready:
                                    git tag v1.3.0
                                            │
                                            ▼
                                    ┌──────────────┐
                                    │  1.3.0 ✅    │ ← NuGet.org stable
                                    └──────────────┘
                                            │
                                     next merges → 1.3.1-pre.1, …
```

### Pre-release packages (GitHub Packages)

Every merge into `main` triggers a CI workflow that:

1. Runs GitVersion to compute the new pre-release version (e.g. `1.2.3-pre.5`).
2. Builds and packs all NuGet packages with that version.
3. Publishes the packages to the **GitHub Packages** feed for this repository.

Pre-release packages are intended for integration testing and early feedback. They are not considered stable.

### Preview packages (NuGet.org pre-release)

When a milestone is approaching readiness, a maintainer may publish a public preview to NuGet.org by pushing a **preview tag**:

```bash
git tag v1.3.0-preview.1
git push origin v1.3.0-preview.1
```

This allows early adopters to test against a stable-ish snapshot without consuming the full GitHub Packages feed.

### Stable releases (NuGet.org)

A stable release is produced by pushing a **git tag** to `main`:

```bash
git tag v1.3.0
git push origin v1.3.0
```

The tag triggers a separate CI workflow that:

1. Verifies GitVersion computes the clean version `1.3.0` (no pre-release suffix).
2. Builds, packs, signs, and publishes all packages to **NuGet.org**.
3. Creates a GitHub Release with auto-generated release notes.

> Tags must follow the `v{MAJOR}.{MINOR}.{PATCH}` format (e.g. `v1.3.0`). Do not tag commits on feature or bugfix branches.

---

## Version Control Rules by Segment

| Segment | Who controls it | How |
|---|---|---|
| `PATCH` | Automatic (GitVersion) | Increments on every merge to `main` since the last tag |
| `MINOR` | Maintainer | Push a `vX.Y.0` tag when all planned milestone features are merged and stable |
| `MAJOR` | Maintainer | Push a `vX.0.0` tag when breaking changes are ready to release |

---

## Milestone Planning vs. Published Version

Milestone labels (e.g. "1.3.0") are **planning intent**, not published version truth.

- Features planned for milestone `1.3.0` are merged to `main` as regular patches.
- Before the tag `v1.3.0` is pushed, those features ship as `1.2.x-pre.*` on GitHub Packages.
- The stable version `1.3.0` exists for consumers only after the maintainer pushes `git tag v1.3.0`.

---

## Examples

### Bug fix: `1.2.0` → `1.2.1`

```
main is at v1.2.0

1. Open issue #99: "Null reference when payload is empty"
2. Create branch: bugfix/99-fix-null-reference
3. Fix + regression test
4. PR title: fix(publisher): resolve null reference on empty payload
5. PR merged → GitVersion computes 1.2.1-pre.1 → published to GitHub Packages
6. Maintainer pushes git tag v1.2.1 → 1.2.1 published to NuGet.org
```

### Feature milestone: `1.2.x` → `1.3.0`

```
main is at v1.2.0, milestone "1.3.0" has multiple features planned

1. Features are implemented across multiple PRs and merged to main
2. During development, CI publishes: 1.2.1-pre.1, 1.2.1-pre.2, ...
3. When all milestone features are merged and validated:
4. Maintainer pushes git tag v1.3.0 → 1.3.0 published to NuGet.org
5. Next merge to main → 1.3.1-pre.1
```

### Breaking change: `1.x` → `2.0.0`

```
main is at v1.x, breaking changes are implemented across one or more PRs

1. Breaking change PRs are merged to main (title: feat!: ... or any prefix)
2. CI publishes 1.2.x-pre.* builds to GitHub Packages during development
3. When all v2 breaking changes are merged and validated:
4. Maintainer pushes git tag v2.0.0 → 2.0.0 published to NuGet.org
```

---

## Consuming Pre-release Packages

To consume a pre-release build from GitHub Packages, add the feed to your `NuGet.config`:

```xml
<configuration>
  <packageSources>
    <add key="deveel-events-preview"
         value="https://nuget.pkg.github.com/deveel/index.json" />
  </packageSources>
</configuration>
```

You will need a GitHub Personal Access Token (PAT) with `read:packages` scope configured as a credential for that source.

