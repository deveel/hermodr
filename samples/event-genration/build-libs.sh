#!/bin/bash
# Build script for EventGeneration.Console dependencies
# This script calls the core build script with the required projects
# The core build script handles all the compilation and copying logic

set -e

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
LIBS_DIR="$SCRIPT_DIR/libs"
CORE_SCRIPT="$SCRIPT_DIR/../build-libs-core.sh"

echo "Building dependencies for EventGeneration.Console..."

# Call the core build script with the projects needed for this sample
"$CORE_SCRIPT" "$LIBS_DIR" \
    "$SCRIPT_DIR/../../src/Deveel.Events.Annotations/Deveel.Events.Annotations.csproj" \
    "$SCRIPT_DIR/../../src/Deveel.Events.Publisher.MassTransit/Deveel.Events.Publisher.MassTransit.csproj" \
    "$SCRIPT_DIR/../../src/Deveel.Events.Publisher/Deveel.Events.Publisher.csproj" \
    "$SCRIPT_DIR/../../src/Deveel.Events.Generators/Deveel.Events.Generators.csproj"

echo ""
echo "You can now reference these binaries from your projects."
echo "Note: Deveel.Events.Generators.dll is copied to libs and used as an analyzer reference."





