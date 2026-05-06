#!/usr/bin/env pwsh

# Generate documentation using DefaultDocumentation CLI tool
# This script generates API documentation for all projects in the Deveel.Events solution

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = $scriptDir
$config = if ($args.Count -gt 0) { $args[0] } else { "Release" }
$docsDir = Join-Path $solutionDir "docs" "api"

Write-Host "🔨 Building solution to generate XML documentation..." -ForegroundColor Cyan
Write-Host "   Configuration: $config" -ForegroundColor Gray

dotnet build (Join-Path $solutionDir "Deveel.Events.sln") -c $config --no-incremental

Write-Host ""
Write-Host "📦 Ensuring DefaultDocumentation.Console tool is installed..." -ForegroundColor Cyan

# Install tool if not present
$toolList = dotnet tool list -g
if ($toolList -notmatch "defaultdocumentation.console") {
    Write-Host "   Installing DefaultDocumentation.Console..." -ForegroundColor Yellow
    dotnet tool install -g DefaultDocumentation.Console
} else {
    Write-Host "   DefaultDocumentation.Console already installed" -ForegroundColor Gray
}

Write-Host ""
Write-Host "📚 Running DefaultDocumentation tool..." -ForegroundColor Cyan
Write-Host "   This processes the assembly files to generate markdown documentation" -ForegroundColor Gray

# Run the DefaultDocumentation tool for each project
$projectFiles = Get-ChildItem -Path (Join-Path $solutionDir "src") -Filter "*.csproj" -Recurse -Depth 1 | Sort-Object FullName

foreach ($projectFile in $projectFiles) {
    $projectDir = $projectFile.Directory
    $projectName = $projectDir.Name
    
    # Find the generated DLL (assembly) file - use the highest target framework
    $dllFile = Get-ChildItem -Path (Join-Path $projectDir.FullName "bin/$config") -Filter "${projectName}.dll" -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1
    
    if ($dllFile) {
        Write-Host "📝 $projectName" -ForegroundColor Green
        
        $outputDir = Join-Path $docsDir $projectName
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        
        # Run DefaultDocumentation CLI
        # -a: Assembly file path (DLL)
        # -o: Output directory
        dotnet tool run defaultdocumentation -- `
            -a $dllFile.FullName `
            -o $outputDir 2>$null `
            || Write-Host "   ⚠️  Skipped (processing error)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "✅ Documentation generation complete!" -ForegroundColor Green
Write-Host "📍 Documentation generated in: $docsDir" -ForegroundColor Cyan






