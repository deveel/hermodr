@echo off
REM Core build script for Deveel.Events library dependencies
REM This script is called by individual sample build scripts with arguments
REM Usage: build-libs-core.bat <output-dir> <project1-csproj> [project2-csproj] ...
REM Example: build-libs-core.bat ./libs ../../src/Deveel.Events.Publisher/Deveel.Events.Publisher.csproj ...

setlocal EnableDelayedExpansion

if "%~1"=="" (
    echo Usage: %0 ^<output-dir^> ^<project1-csproj^> [project2-csproj] ...
    echo.
    echo Example:
    echo   %0 .\libs ..\..\src\Deveel.Events.Annotations\Deveel.Events.Annotations.csproj
    exit /b 1
)

set LIBS_DIR=%~1
shift
set TEMP_DIR=%TEMP%\deveel-events-build-%RANDOM%
set BUILD_INDEX=1
set PROJECT_COUNT=0

REM Count projects
for %%A in (%*) do set /a PROJECT_COUNT+=1

echo Building Deveel.Events library dependencies...
echo Output directory: %LIBS_DIR%
echo Temporary build directory: %TEMP_DIR%
echo Projects to build: %PROJECT_COUNT%

REM Create libs directory if it doesn't exist
if not exist "%LIBS_DIR%" mkdir "%LIBS_DIR%"

REM Clean the libs directory
if exist "%LIBS_DIR%" (
    for /f %%f in ('dir /b "%LIBS_DIR%"') do (
        del /q "%LIBS_DIR%\%%f" >nul 2>&1
    )
)

REM Create temp directory
if not exist "%TEMP_DIR%" mkdir "%TEMP_DIR%"

REM Build each project
for %%A in (%*) do (
    set "PROJECT_PATH=%%A"
    for %%F in ("!PROJECT_PATH!") do set "PROJECT_NAME=%%~nxF"
    
    echo.
    echo [!BUILD_INDEX!/%PROJECT_COUNT%] Building !PROJECT_NAME!...
    
    set "TEMP_OUTPUT=%TEMP_DIR%\build-!BUILD_INDEX!"
    if not exist "!TEMP_OUTPUT!" mkdir "!TEMP_OUTPUT!"
    
    dotnet build "!PROJECT_PATH!" --configuration Release --framework net9.0 --output "!TEMP_OUTPUT!" --nologo
    if errorlevel 1 (
        dotnet build "!PROJECT_PATH!" --configuration Release --output "!TEMP_OUTPUT!" --nologo
        if errorlevel 1 (
            rmdir /s /q "%TEMP_DIR%" >nul 2>&1
            exit /b 1
        )
    )
    
    for /r "!TEMP_OUTPUT!" %%f in (*.dll) do copy "%%f" "%LIBS_DIR%\" >nul
    
    set /a BUILD_INDEX+=1
)

REM Clean up temporary directory
rmdir /s /q "%TEMP_DIR%" >nul 2>&1

echo.
echo Build complete! Binaries are located in: %LIBS_DIR%
for /f %%A in ('dir /b "%LIBS_DIR%\*.dll" 2^>nul ^| find /c /v ""') do set "DLL_COUNT=%%A"
echo   Total DLLs copied: !DLL_COUNT!

endlocal


