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
| `MAJOR` | Incompatible API or architectural changes are introduced |
| `MINOR` | New, backward-compatible features are added |
| `PATCH` | Backward-compatible bug fixes are delivered |

---

## Automated Version Calculation — GitVersion

Version numbers are computed automatically by [GitVersion](https://gitversion.net/) from the git history.  No manual version editing is required.  The two inputs that drive the calculation are:

1. **The last git tag** on `main` — establishes the baseline version.
2. **The PR title** (used as the squash-merge commit message on `main`) — determines whether the next segment bumped is major, minor, or patch.

### Conventional Commits — PR title format

PR titles **must** follow the [Conventional Commits](https://www.conventionalcommits.org/) specification.  GitHub uses the PR title as the squash-merge commit message, so the title directly controls the automatic version bump.

| PR title format | Version bump | Example |
|---|---|---|
| `fix: <description>` | **Patch** — `x.y.Z` | `fix: resolve null reference on empty payload` |
| `fix(<scope>): <description>` | **Patch** — `x.y.Z` | `fix(publisher): resolve null reference on empty payload` |
| `feat: <description>` | **Minor** — `x.Y.0` | `feat: add dead-letter channel` |
| `feat(<scope>): <description>` | **Minor** — `x.Y.0` | `feat(schema): add AsyncAPI export support` |
| `feat!: <description>` | **Major** — `X.0.0` | `feat!: redesign IEventPublisher interface` |
| `BREAKING CHANGE: <description>` | **Major** — `X.0.0` | (body of commit) |
| `docs:`, `chore:`, `ci:`, `test:`, `build:` | **No bump** | `docs: update contributing guide` |

> **Tip:** The `<scope>` is optional and refers to the package or subsystem affected, e.g. `publisher`, `schema`, `outbox`.

---

## Branch Version Labels

While work is in progress on a branch, GitVersion produces a pre-release version to identify CI builds.  These are **not** published as public releases.

| Branch type | Naming convention | Example branch | Version produced |
|---|---|---|---|
| `main` | — | `main` | `1.3.1-pre.2` |
| Feature / enhancement | `<issue-number>-<slug>` | `123-add-dead-letter-channel` | `1.4.0-alpha.5` |
| Bug fix | `bugfix/<issue-number>-<slug>` | `bugfix/99-fix-null-reference` | `1.3.1-bugfix.3` |
| Pull request | auto | PR #42 | `1.3.1-pr.1` |

The number at the end of the pre-release tag is the **commit count since the last stable tag** on that branch.

---

## Release Lifecycle

```
     git tag v1.3.0
          │
          ▼
    ┌─────────────┐   fix(publisher): ...   ┌──────────────┐
    │ main 1.3.0  │ ──────────────────────► │ 1.3.1-pre.1  │
    └─────────────┘                         └──────┬───────┘
                                                   │ (CI publishes pre-release
                                                   │  to GitHub Packages)
                                                   │
                                     more merges   │
                                                   ▼
                                           ┌──────────────┐
                                           │ 1.3.1-pre.2  │
                                           └──────┬───────┘
                                                  │
                                    git tag v1.3.1│
                                                  ▼
                                           ┌──────────────┐
                                           │  1.3.1 ✅    │  ← published to NuGet.org
                                           └──────────────┘
```

### Pre-release packages (GitHub Packages)

Every merge into `main` triggers a CI workflow that:

1. Runs GitVersion to compute the new pre-release version (e.g. `1.3.1-pre.2`).
2. Builds and packs all NuGet packages with that version.
3. Publishes the packages to the **GitHub Packages** feed for this repository.

Pre-release packages are intended for integration testing and early feedback.  They are not considered stable.

### Stable releases (NuGet.org)

A stable release is produced by pushing a **git tag** to `main`:

```bash
git tag v1.3.1
git push origin v1.3.1
```

The tag triggers a separate CI workflow that:

1. Verifies GitVersion computes the clean version `1.3.1` (no pre-release suffix).
2. Builds, packs, signs, and publishes all packages to **NuGet.org**.
3. Creates a GitHub Release with auto-generated release notes.

> Tags must follow the `v{MAJOR}.{MINOR}.{PATCH}` format (e.g. `v1.3.1`).  Do not tag commits on feature or bugfix branches.

---

## Version Bump Rules by Workflow

| Event | Who merges | When | Version bump | Release |
|---|---|---|---|---|
| Bug fix PR approved | Maintainer | Immediately | **Patch** (`x.y.Z`) | Pre-release auto-published; stable after tagging |
| Feature PR approved | Maintainer | When `main` reaches the target major version | **Minor** (`x.Y.0`) | Pre-release auto-published; stable after tagging |
| Breaking change PR approved | Maintainer | Major version milestone | **Major** (`X.0.0`) | Pre-release auto-published; stable after tagging |

---

## Examples

### Bug fix: `1.3.0` → `1.3.1`

```
main is at v1.3.0

1. Open issue #99: "Null reference when payload is empty"
2. Create branch: bugfix/99-fix-null-reference
3. Fix + regression test
4. PR title: fix(publisher): resolve null reference on empty payload
5. PR merged → GitVersion computes 1.3.1-pre.1 → published to GitHub Packages
6. Maintainer pushes git tag v1.3.1 → 1.3.1 published to NuGet.org
```

### Feature: `1.3.0` → `1.4.0`

```
main is at v1.3.0  (on the v1 release train)

1. Open issue #123: "Add dead-letter channel"
2. Create branch: 123-add-dead-letter-channel
3. Implement feature
4. PR stays open (or is kept in draft) until v1.4 milestone is active
5. PR title: feat(publisher): add dead-letter channel
6. PR merged → GitVersion computes 1.4.0-pre.1 → published to GitHub Packages
7. Maintainer pushes git tag v1.4.0 → 1.4.0 published to NuGet.org
```

### Breaking change: `1.x` → `2.0.0`

```
main is at v1.x (feature branches for v2 exist but are not yet merged)

1. All v2.x feature branches are reviewed and approved
2. Each is merged to main with feat!: or feat: PR titles as appropriate
3. When all v2 features are merged: git tag v2.0.0
4. 2.0.0 published to NuGet.org
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

