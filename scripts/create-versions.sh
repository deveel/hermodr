#!/bin/bash

# Script to create Docusaurus versions from git tags
# Versions: v1.2.3, v1.2.4, v1.2.5, v1.2.6, v1.2.7

set -e

TAGS=("v1.2.3" "v1.2.4" "v1.2.5" "v1.2.6" "v1.2.7")
WEBSITE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../website" && pwd)"
DOCS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../docs" && pwd)"

echo "🚀 Starting Docusaurus version creation..."
echo "Working directory: $WEBSITE_DIR"
echo "Docs directory: $DOCS_DIR"

# Store current branch
CURRENT_BRANCH=$(git branch --show-current)
echo "Current branch: $CURRENT_BRANCH"

# Initialize versions array
echo "[]" > "$DOCS_DIR/versions.json"

cd "$WEBSITE_DIR"

for TAG in "${TAGS[@]}"; do
    echo ""
    echo "📦 Creating version $TAG..."
    
    # Checkout the tag
    echo "  Checking out $TAG..."
    git checkout "$TAG" --quiet
    
    # Create temporary directory for old docs
    TEMP_DOCS=$(mktemp -d)
    cp -r "$DOCS_DIR"/* "$TEMP_DOCS/"
    
    # Go back to main branch
    git checkout "$CURRENT_BRANCH" --quiet
    
    # Create versioned docs directory
    VERSION_DIR="$DOCS_DIR/versioned_docs/version-$TAG"
    VERSIONED_SIDEBARS="$DOCS_DIR/versioned_sidebars/version-$TAG-sidebars.ts"
    
    echo "  Creating $VERSION_DIR..."
    mkdir -p "$VERSION_DIR"
    
    # Copy old docs to versioned directory
    cp -r "$TEMP_DOCS"/* "$VERSION_DIR/"
    
    # Copy current sidebars (will be adjusted for old structure)
    cp "$WEBSITE_DIR/sidebars.ts" "$VERSIONED_SIDEBARS"
    
    # Clean up temp directory
    rm -rf "$TEMP_DOCS"
    
    # Run docusaurus docs:version command
    echo "  Running docusaurus docs:version $TAG..."
    npm run docusaurus docs:version "$TAG"
    
    # Add to versions.json
    echo "  Updating versions.json..."
    jq --arg tag "$TAG" '. += [$tag]' "$DOCS_DIR/versions.json" > "$DOCS_DIR/versions.json.tmp"
    mv "$DOCS_DIR/versions.json.tmp" "$DOCS_DIR/versions.json"
    
    echo "  ✅ Version $TAG created successfully"
done

# Restore current branch (should already be on it, but just in case)
git checkout "$CURRENT_BRANCH" --quiet

echo ""
echo "✅ All versions created successfully!"
echo "Versions: ${TAGS[*]}"
echo ""
echo "Next steps:"
echo "1. Review the created versions in docs/versioned_docs/"
echo "2. Test with: npm run serve"
echo "3. Commit the changes"
