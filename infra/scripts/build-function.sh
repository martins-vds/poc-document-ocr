#!/bin/bash
set -e

echo "================================"
echo "Building Azure Function App"
echo "================================"

# Navigate to the source directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC_DIR="$PROJECT_ROOT/src/DocumentOcrProcessor"

cd "$SRC_DIR"

echo ""
echo "Step 1: Cleaning previous builds..."
dotnet clean

echo ""
echo "Step 2: Restoring NuGet packages..."
dotnet restore

echo ""
echo "Step 3: Building the project..."
dotnet build --configuration Release

echo ""
echo "Step 4: Publishing the function app..."
dotnet publish --configuration Release --output ./publish

echo ""
echo "================================"
echo "Build completed successfully!"
echo "Published to: $SRC_DIR/publish"
echo "================================"
