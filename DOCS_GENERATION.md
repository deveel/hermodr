# Documentation Generation Scripts

These scripts generate API documentation for the Deveel.Events solution using the **DefaultDocumentation.Console** global dotnet tool, independently of the normal build process.

## Overview

The Deveel.Events solution uses DefaultDocumentation (configured in `src/Directory.Build.props`) to generate Markdown documentation from XML comments in code. These scripts invoke the `dotnet tool run defaultdocumentation` CLI for all projects in the solution.

## Scripts

### Main Scripts (Recommended)

#### `generate-docs.sh` (macOS/Linux)
```bash
./generate-docs.sh          # Uses Release configuration
./generate-docs.sh Debug    # Uses Debug configuration
```

**What it does:**
- Builds the solution in the specified configuration
- Ensures DefaultDocumentation.Console tool is installed globally
- Runs `dotnet tool run defaultdocumentation` for each compiled assembly
- Generates Markdown documentation in `docs/api/[ProjectName]`

#### `generate-docs.ps1` (Windows PowerShell)
```powershell
.\generate-docs.ps1         # Uses Release configuration
.\generate-docs.ps1 Debug   # Uses Debug configuration
```

**What it does:**
- Same as `.sh` script with PowerShell syntax
- Better integration with Windows environment

### How the CLI Tool Works

The scripts call:
```bash
dotnet tool run defaultdocumentation -- -a <assembly.dll> -o <output-directory>
```

Where:
- `-a` or `--AssemblyFilePath`: Path to the compiled assembly (.dll) file
- `-o` or `--OutputDirectoryPath`: Output directory for generated markdown documentation

The tool automatically finds the corresponding XML documentation file (assuming it's in the same directory as the .dll, as configured by `GenerateDocumentationFile` in `Directory.Build.props`).

## Output

Documentation is generated in the `docs/api/` directory, organized by project:
```
docs/api/
├── Deveel.Events/
├── Deveel.Events.Annotations/
├── Deveel.Events.Publisher/
└── ... (one directory per source project)
```

Each directory contains Markdown files documenting that project's public API.

## Configuration

### Build Configuration

The generation process respects your build configuration:
- **Release** (default): Uses Release builds which are typically optimized
- **Debug**: Uses Debug builds (useful for debugging documentation generation)

### Documentation Settings

Documentation generation is configured in `/src/Directory.Build.props`:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<DefaultDocumentationFolder>$(SolutionDir)docs/api/$(MSBuildProjectName)</DefaultDocumentationFolder>
<DefaultDocumentationFileNameFactory>NameAndMd5Mix</DefaultDocumentationFileNameFactory>
<DefaultDocumentationGeneratedAccessModifiers>Public</DefaultDocumentationGeneratedAccessModifiers>
```

These settings control:
- XML documentation generation
- Output location per project
- Filename generation method
- Access modifiers to document (public only)

## Prerequisites

- .NET 8.0 or later (targets: `net8.0;net9.0;net10.0`)
- `dotnet` CLI in your PATH
- macOS/Linux with Bash/Zsh (for `.sh` scripts)
- Windows with PowerShell (for `.ps1` scripts)

The tool will be automatically installed globally on your machine if not already present.

## Performance

- **First run** (with full build): ~30-45 seconds
- **Subsequent runs** (build artifacts cached): ~10-15 seconds
- **Incremental build** (assembly files unchanged): ~5-10 seconds

## Troubleshooting

### "No markdown files generated"
1. Verify the build succeeded: `dotnet build Deveel.Events.sln -c Release`
2. Check if XML documentation files exist:
   ```bash
   find src -name "*.xml" -type f
   ```
3. Verify projects have `GenerateDocumentationFile` enabled in `Directory.Build.props`

### "DefaultDocumentation.Console not found"
The tool is normally installed automatically. To manually install:
```bash
dotnet tool install -g DefaultDocumentation.Console
```

### Permission denied (macOS/Linux)
Make scripts executable:
```bash
chmod +x generate-docs.sh
chmod +x generate-docs-msbuild.sh
```

### Build fails
- Ensure all dependencies are restored: `dotnet restore`
- Check for build errors: `dotnet build Deveel.Events.sln`
- See build output in `bin/` and `obj/` directories

## Alternative Approaches

### Direct CLI Usage

Generate documentation for a single project:
```bash
dotnet tool run defaultdocumentation -- \
  -a "src/Deveel.Events/bin/Release/net8.0/Deveel.Events.dll" \
  -o "docs/api/Deveel.Events"
```

### Build-Time Generation

Documentation runs automatically during builds:
```bash
dotnet build Deveel.Events.sln -c Release
```

The DefaultDocumentation MSBuild target runs as part of the build, so documentation in `docs/api/` is updated whenever you build.

### Using MSBuild Directly

For more control with explicit MSBuild target invocation:
```bash
./generate-docs-msbuild.sh          # macOS/Linux
.\generate-docs-msbuild.ps1         # Windows
```

These use the MSBuild `DefaultDocumentation` target instead of the CLI tool.

## Integration with CI/CD

### GitHub Actions Example

```yaml
- name: Build and Generate Documentation
  run: |
    chmod +x ./generate-docs.sh
    ./generate-docs.sh Release
    
- name: Upload Documentation
  uses: actions/upload-artifact@v3
  with:
    name: api-docs
    path: docs/api/
```

## File Structure After Generation

```
Deveel.Events/                     # Example project
├── README.md                      # Main namespace documentation
├── Deveel.Events.md              # Namespace overview
├── ClassName.md                  # Class documentation
├── ClassName.PropertyName.md     # Property documentation
├── ClassName.MethodName(...).md  # Method documentation
└── ...
```

## Command Reference

| Command | Purpose |
|---------|---------|
| `./generate-docs.sh` | Generate docs (Release config) |
| `./generate-docs.sh Debug` | Generate docs (Debug config) |
| `./generate-docs.ps1` | Generate docs (Windows, Release) |
| `./generate-docs.ps1 Debug` | Generate docs (Windows, Debug) |
| `./generate-docs-msbuild.sh` | Generate using MSBuild target |
| `./generate-docs-msbuild.ps1` | Generate using MSBuild (Windows) |

## Notes

- Scripts automatically skip projects if compiled DLL files don't exist
- The tool uses the highest target framework found (net10.0 > net9.0 > net8.0)
- Only public members are documented (configured in `Directory.Build.props`)
- Documentation is incremental - run again to update with latest code changes
