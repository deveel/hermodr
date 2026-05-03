# Core build script for Deveel.Events library dependencies
# This script is called by individual sample build scripts with arguments
# Usage: .\build-libs-core.ps1 -OutputDir <output-dir> -Projects @("project1.csproj", "project2.csproj", ...)
# Example: .\build-libs-core.ps1 -OutputDir ./libs -Projects @("../../src/Deveel.Events.Annotations/Deveel.Events.Annotations.csproj")

param(
    [Parameter(Mandatory=$true)]
    [string]$OutputDir,
    
    [Parameter(Mandatory=$true)]
    [string[]]$Projects
)

$ErrorActionPreference = "Stop"

$LibsDir = $OutputDir
$TempDir = Join-Path -Path $env:TEMP -ChildPath "deveel-events-build-$(Get-Random)"

Write-Host "Building Deveel.Events library dependencies..."
Write-Host "Output directory: $LibsDir"
Write-Host "Temporary build directory: $TempDir"
Write-Host "Projects to build: $($Projects.Count)"

# Create libs directory if it doesn't exist
if (-not (Test-Path $LibsDir)) {
    New-Item -ItemType Directory -Path $LibsDir | Out-Null
}

# Clean the libs directory
if (Test-Path $LibsDir) {
    Remove-Item -Path (Join-Path -Path $LibsDir -ChildPath "*") -Force -ErrorAction SilentlyContinue
}

# Create temp directory
New-Item -ItemType Directory -Path $TempDir | Out-Null

try {
    # Build each project
    $BuildIndex = 1
    foreach ($ProjectPath in $Projects) {
        # Extract project name
        $ProjectName = Split-Path -Path $ProjectPath -Leaf
        
        Write-Host ""
        Write-Host "[$BuildIndex/$($Projects.Count)] Building $ProjectName..."
        
        # Create temporary output directory for this build
        $TempOutput = Join-Path -Path $TempDir -ChildPath "build-$BuildIndex"
        New-Item -ItemType Directory -Path $TempOutput | Out-Null
        
        # Build net9.0 assets when available; fallback keeps non-net9 projects supported.
        & dotnet build $ProjectPath --configuration Release --framework net9.0 --output $TempOutput --nologo
        if ($LASTEXITCODE -ne 0) {
            & dotnet build $ProjectPath --configuration Release --output $TempOutput --nologo
            if ($LASTEXITCODE -ne 0) { exit 1 }
        }
        
        # Copy DLLs to libs folder
        Get-ChildItem -Path $TempOutput -Filter "*.dll" -File | ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $LibsDir -Force
        }
        
        $BuildIndex++
    }

    $DllCount = (Get-ChildItem -Path $LibsDir -Filter "*.dll" -File | Measure-Object).Count
    Write-Host ""
    Write-Host "✓ Build complete! Binaries are located in: $LibsDir"
    Write-Host "  Total DLLs copied: $DllCount"
}
finally {
    # Clean up temporary directory
    if (Test-Path $TempDir) {
        Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}


