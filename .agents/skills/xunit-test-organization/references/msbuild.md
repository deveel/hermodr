# MSBuild and Repository Layout References

This reference supports the skill's guidance on tests-folder-scoped test project
configuration and package centralization.

## Official Resources

- Customize builds by folder with `Directory.Build.props`: <https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory>
- MSBuild reference: <https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild>

## Skill-specific interpretation

- Keep shared test package injection centralized in `test/Directory.Build.props`
- Keep executable test projects minimal by default: target framework plus
  project references, adding local `PackageReference` entries only for
  project-specific needs
- Use `.XUnit` for runnable test projects and `.Testing` for shared test support libraries
- Set `<IsTestProject>false</IsTestProject>` only for shared support libraries, not executable test projects
- Place test projects under `tests/` and production code under `src/`

## Why this matters

Centralized test-scoped build configuration reduces drift between test projects,
keeps package choices consistent, and avoids leaking test-only settings into
`src/` projects.

