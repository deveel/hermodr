# Build script for OrderService (outbox-relay) dependencies
# This script calls the core build script with the required projects
# The core build script handles all the compilation and copying logic
# This sample has two projects: OrderService.Api and OrderService.RelayWorker

param()

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$LibsDir = Join-Path -Path $ScriptDir -ChildPath "libs"
$CoreScript = Join-Path -Path $ScriptDir -ChildPath "..\build-libs-core.ps1"

Write-Host "Building dependencies for OrderService (outbox-relay sample)..."

# Call the core build script with the projects needed for this sample
& $CoreScript `
    -OutputDir $LibsDir `
    -Projects @(
        (Join-Path -Path $ScriptDir -ChildPath "..\..\src\Deveel.Events.Publisher.Outbox.EntityFramework\Deveel.Events.Publisher.Outbox.EntityFramework.csproj"),
        (Join-Path -Path $ScriptDir -ChildPath "..\..\src\Deveel.Events.Publisher.MassTransit\Deveel.Events.Publisher.MassTransit.csproj"),
        (Join-Path -Path $ScriptDir -ChildPath "..\..\src\Deveel.Events.Annotations\Deveel.Events.Annotations.csproj"),
        (Join-Path -Path $ScriptDir -ChildPath "..\..\src\Deveel.Events.Publisher.Outbox\Deveel.Events.Publisher.Outbox.csproj"),
        (Join-Path -Path $ScriptDir -ChildPath "..\..\src\Deveel.Events.Publisher\Deveel.Events.Publisher.csproj")
    )

Write-Host ""
Write-Host "You can now reference these binaries from your projects."
Write-Host "Both OrderService.Api and OrderService.RelayWorker can reference the libs folder."





