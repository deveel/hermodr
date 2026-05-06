#!/bin/bash
# Core build script for Deveel.Events library dependencies
# This script is called by individual sample build scripts with arguments
# Usage: build-libs-core.sh <output-dir> <project1-csproj> [project2-csproj] ...
# Example: build-libs-core.sh ./libs ../../src/Deveel.Events.Publisher/Deveel.Events.Publisher.csproj ...

set -e

if [ $# -lt 2 ]; then
    echo "Usage: $0 <output-dir> <project1-csproj> [project2-csproj] ..."
    echo ""
    echo "Example:"
    echo "  $0 ./libs ../../src/Deveel.Events.Annotations/Deveel.Events.Annotations.csproj ../../src/Deveel.Events.Publisher/Deveel.Events.Publisher.csproj"
    exit 1
fi

LIBS_DIR="$1"
shift
PROJECTS=("$@")

TEMP_DIR=$(mktemp -d)

echo "Building Deveel.Events library dependencies..."
echo "Output directory: $LIBS_DIR"
echo "Temporary build directory: $TEMP_DIR"
echo "Projects to build: ${#PROJECTS[@]}"

# Create libs directory if it doesn't exist
mkdir -p "$LIBS_DIR"

# Clean the libs directory
rm -rf "$LIBS_DIR"/*
mkdir -p "$LIBS_DIR"

# Build each project
BUILD_INDEX=1
for PROJECT_PATH in "${PROJECTS[@]}"; do
    # Extract project name from path
    PROJECT_NAME=$(basename "$(dirname "$PROJECT_PATH")")
    
    echo ""
    echo "[$BUILD_INDEX/${#PROJECTS[@]}] Building $PROJECT_NAME..."
    
    # Create temporary output directory for this build
    TEMP_OUTPUT="$TEMP_DIR/build-$BUILD_INDEX"
    
    # Build net9.0 assets when available; fallback keeps non-net9 projects supported.
    if dotnet build "$PROJECT_PATH" --configuration Release --framework net9.0 --output "$TEMP_OUTPUT" --nologo; then
        :
    else
        dotnet build "$PROJECT_PATH" --configuration Release --output "$TEMP_OUTPUT" --nologo
    fi
    
    # Copy DLLs to libs folder
    if [ -d "$TEMP_OUTPUT" ]; then
        find "$TEMP_OUTPUT" -maxdepth 1 -name "*.dll" -type f -exec cp {} "$LIBS_DIR/" \;
    fi
    
    ((BUILD_INDEX++))
done

# Clean up temporary directory
rm -rf "$TEMP_DIR"

echo ""
echo "✓ Build complete! Binaries are located in: $LIBS_DIR"
echo "  Total DLLs copied: $(find "$LIBS_DIR" -maxdepth 1 -name "*.dll" -type f | wc -l)"


