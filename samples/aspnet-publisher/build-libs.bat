@echo off
REM Build script for OrderService.SimplePublisher dependencies
REM This script calls the core build script with the required projects
REM The core build script handles all the compilation and copying logic

setlocal EnableDelayedExpansion

set SCRIPT_DIR=%~dp0
set LIBS_DIR=%SCRIPT_DIR%libs
set CORE_SCRIPT=%SCRIPT_DIR%..\build-libs-core.bat

echo Building dependencies for OrderService.SimplePublisher...

REM Call the core build script with the projects needed for this sample
call "%CORE_SCRIPT%" "%LIBS_DIR%" ^
    "%SCRIPT_DIR%..\..\src\Deveel.Events.Publisher.RabbitMq\Deveel.Events.Publisher.RabbitMq.csproj" ^
    "%SCRIPT_DIR%..\..\src\Deveel.Events.Annotations\Deveel.Events.Annotations.csproj" ^
    "%SCRIPT_DIR%..\..\src\Deveel.Events.Amqp.Annotations\Deveel.Events.Amqp.Annotations.csproj" ^
    "%SCRIPT_DIR%..\..\src\Deveel.Events.Publisher\Deveel.Events.Publisher.csproj"

echo.
echo You can now reference these binaries from your projects.





