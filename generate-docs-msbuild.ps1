#!/usr/bin/env pwsh

# Advanced documentation generation using MSBuild
# This approach directly invokes the DefaultDocumentation MSBuild target
# Faster than full rebuild, uses only the configurations from Directory.Build.props

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionFile = Join-Path $scriptDir "Deveel.Events.sln"
$config = if ($args.Count -gt 0) { $args[0] } else { "Release" }

Write-Host "🔨 Building solution to generate XML documentation..." -ForegroundColor Cyan
Write-Host "   Configuration: $config" -ForegroundColor Gray
Write-Host "   This may take a minute..." -ForegroundColor Gray

# First, do a quick build to ensure all XML documentation is generated
dotnet build $solutionFile -c $config --no-incremental -v quiet

Write-Host ""
Write-Host "📚 Running DefaultDocumentation MSBuild target..." -ForegroundColor Cyan

# Run the DefaultDocumentation target for each project using Get-ChildItem
$projectFiles = Get-ChildItem -Path (Join-Path $scriptDir "src") -Filter "*.csproj" -Recurse -Depth 1 | Sort-Object FullName

foreach ($projectFile in $projectFiles) {
    $projectName = $projectFile.Directory.Name
    Write-Host "📝 $projectName" -ForegroundColor Green
    
    dotnet msbuild $projectFile.FullName `
        -t:DefaultDocumentation `
        -c $config `
        -p:DesignTimeBuild=false `
        -v quiet
}

Write-Host ""
Write-Host "✅ Documentation generation complete!" -ForegroundColor Green
Write-Host "📍 Check docs/api/ for generated documentation" -ForegroundColor Cyan


