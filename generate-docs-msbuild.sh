#!/bin/zsh

# Advanced documentation generation using MSBuild
# This approach directly invokes the DefaultDocumentation MSBuild target
# Faster than full rebuild, uses only the configurations from Directory.Build.props

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_FILE="$SCRIPT_DIR/Deveel.Events.sln"
CONFIG="${1:-Release}"

echo "🔨 Building solution to generate XML documentation..."
echo "   Configuration: $CONFIG"
echo "   This may take a minute..."

# First, do a quick build to ensure all XML documentation is generated
dotnet build "$SOLUTION_FILE" -c "$CONFIG" --no-incremental -v quiet

echo ""
echo "📚 Running DefaultDocumentation MSBuild target..."

# Run the DefaultDocumentation target for each project using find
while IFS= read -r project_file; do
    project_dir=$(dirname "$project_file")
    project_name=$(basename "$project_dir")
    
    echo "📝 $project_name"
    
    dotnet msbuild "$project_file" \
        -t:DefaultDocumentation \
        -c "$CONFIG" \
        -p:DesignTimeBuild=false \
        -v quiet
done < <(find "$SCRIPT_DIR/src" -maxdepth 2 -name "*.csproj" -type f | sort)

echo ""
echo "✅ Documentation generation complete!"
echo "📍 Check docs/api/ for generated documentation"


