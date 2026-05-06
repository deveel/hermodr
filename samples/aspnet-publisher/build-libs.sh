#!/bin/bash
# Build script for OrderService.SimplePublisher dependencies
# This script calls the core build script with the required projects
# The core build script handles all the compilation and copying logic

set -e

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
LIBS_DIR="$SCRIPT_DIR/libs"
CORE_SCRIPT="$SCRIPT_DIR/../build-libs-core.sh"

echo "Building dependencies for OrderService.SimplePublisher..."

# Call the core build script with the projects needed for this sample
"$CORE_SCRIPT" "$LIBS_DIR" \
    "$SCRIPT_DIR/../../src/Deveel.Events.Publisher.RabbitMq/Deveel.Events.Publisher.RabbitMq.csproj" \
    "$SCRIPT_DIR/../../src/Deveel.Events.Annotations/Deveel.Events.Annotations.csproj" \
    "$SCRIPT_DIR/../../src/Deveel.Events.Amqp.Annotations/Deveel.Events.Amqp.Annotations.csproj" \
    "$SCRIPT_DIR/../../src/Deveel.Events.Publisher/Deveel.Events.Publisher.csproj"

echo ""
echo "You can now reference these binaries from your projects."





