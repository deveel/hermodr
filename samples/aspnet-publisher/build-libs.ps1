# Build script for OrderService.SimplePublisher dependencies
# This script calls the core build script with the required projects
# The core build script handles all the compilation and copying logic

param()

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$LibsDir = Join-Path -Path $ScriptDir -ChildPath "libs"
$CoreScript = Join-Path -Path $ScriptDir -ChildPath "..\build-libs-core.ps1"

Write-Host "Building dependencies for OrderService.SimplePublisher..."

# Call the core build script with the projects needed for this sample
& $CoreScript `
    -OutputDir $LibsDir `
    -Projects @(
        (Join-Path -Path $ScriptDir -ChildPath "..\..\src\Deveel.Events.Publisher.RabbitMq\Deveel.Events.Publisher.RabbitMq.csproj"),
        (Join-Path -Path $ScriptDir -ChildPath "..\..\src\Deveel.Events.Annotations\Deveel.Events.Annotations.csproj"),
        (Join-Path -Path $ScriptDir -ChildPath "..\..\src\Deveel.Events.Amqp.Annotations\Deveel.Events.Amqp.Annotations.csproj"),
        (Join-Path -Path $ScriptDir -ChildPath "..\..\src\Deveel.Events.Publisher\Deveel.Events.Publisher.csproj")
    )

Write-Host ""
Write-Host "You can now reference these binaries from your projects."





