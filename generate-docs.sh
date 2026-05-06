#!/bin/zsh

# Generate documentation using DefaultDocumentation CLI tool
# This script generates API documentation for all projects in the Deveel.Events solution

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_DIR="$SCRIPT_DIR"
CONFIG="${1:-Release}"
DOCS_DIR="$SOLUTION_DIR/docs/api"

echo "🔨 Building solution to generate XML documentation..."
echo "   Configuration: $CONFIG"

dotnet build "$SOLUTION_DIR/Deveel.Events.sln" -c "$CONFIG" --no-incremental

echo ""
echo "📦 Ensuring DefaultDocumentation.Console tool is installed..."

# Install tool if not present
if ! dotnet tool list -g | grep -q "defaultdocumentation.console"; then
    echo "   Installing DefaultDocumentation.Console..."
    dotnet tool install -g DefaultDocumentation.Console
else
    echo "   DefaultDocumentation.Console already installed"
fi

echo ""
echo "📚 Running DefaultDocumentation tool..."
echo "   This processes the assembly files to generate markdown documentation"

# Run the DefaultDocumentation tool for each project using find
while IFS= read -r project_file; do
    project_dir=$(dirname "$project_file")
    project_name=$(basename "$project_dir")
    
    # Find the generated DLL (assembly) file - use the highest target framework
    dll_file=$(find "$project_dir/bin/$CONFIG" -name "${project_name}.dll" -type f | sort -V | tail -1)
    
    if [ -n "$dll_file" ] && [ -f "$dll_file" ]; then
        echo "📝 $project_name"
        
        output_dir="$DOCS_DIR/$project_name"
        mkdir -p "$output_dir"
        
        # Run DefaultDocumentation CLI
        # -a: Assembly file path (DLL)
        # -o: Output directory
        dotnet tool run defaultdocumentation -- \
            -a "$dll_file" \
            -o "$output_dir" 2>/dev/null || echo "   ⚠️  Skipped (processing error)"
    fi
done < <(find "$SOLUTION_DIR/src" -maxdepth 2 -name "*.csproj" -type f | sort)

echo ""
echo "✅ Documentation generation complete!"
echo "📍 Documentation generated in: $DOCS_DIR"







