@echo off
REM Build script for OrderService (outbox-relay) dependencies
REM This script calls the core build script with the required projects
REM The core build script handles all the compilation and copying logic
REM This sample has two projects: OrderService.Api and OrderService.RelayWorker

setlocal EnableDelayedExpansion

set SCRIPT_DIR=%~dp0
set LIBS_DIR=%SCRIPT_DIR%libs
set CORE_SCRIPT=%SCRIPT_DIR%..\build-libs-core.bat

echo Building dependencies for OrderService (outbox-relay sample)...

REM Call the core build script with the projects needed for this sample
call "%CORE_SCRIPT%" "%LIBS_DIR%" ^
    "%SCRIPT_DIR%..\..\src\Deveel.Events.Publisher.Outbox.EntityFramework\Deveel.Events.Publisher.Outbox.EntityFramework.csproj" ^
    "%SCRIPT_DIR%..\..\src\Deveel.Events.Publisher.MassTransit\Deveel.Events.Publisher.MassTransit.csproj" ^
    "%SCRIPT_DIR%..\..\src\Deveel.Events.Annotations\Deveel.Events.Annotations.csproj" ^
    "%SCRIPT_DIR%..\..\src\Deveel.Events.Publisher.Outbox\Deveel.Events.Publisher.Outbox.csproj" ^
    "%SCRIPT_DIR%..\..\src\Deveel.Events.Publisher\Deveel.Events.Publisher.csproj"

echo.
echo You can now reference these binaries from your projects.
echo Both OrderService.Api and OrderService.RelayWorker can reference the libs folder.
pause





