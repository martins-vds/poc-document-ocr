#!/bin/bash
set -e

echo "================================"
echo "Deploying Azure Function App"
echo "================================"

# Check if function app name is provided
if [ -z "$1" ]; then
  echo "Error: Function app name is required"
  echo "Usage: ./deploy-function.sh <function-app-name>"
  echo ""
  echo "Example: ./deploy-function.sh func-documentocr-dev-abc123"
  exit 1
fi

FUNCTION_APP_NAME=$1

# Navigate to the source directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC_DIR="$PROJECT_ROOT/src"

cd "$SRC_DIR"

echo ""
echo "Function App Name: $FUNCTION_APP_NAME"
echo ""

# Check if Azure Functions Core Tools is available
if ! command -v func &> /dev/null; then
  echo "Error: Azure Functions Core Tools (func) is not installed"
  echo "Please install it from: https://docs.microsoft.com/azure/azure-functions/functions-run-local"
  exit 1
fi

# Check if user is logged into Azure CLI
if ! az account show &> /dev/null; then
  echo "Error: Not logged into Azure CLI"
  echo "Please run: az login"
  exit 1
fi

echo "Step 1: Building the function app..."
dotnet clean
dotnet restore
dotnet build --configuration Release

echo ""
echo "Step 2: Deploying to Azure Functions..."
func azure functionapp publish "$FUNCTION_APP_NAME" --dotnet-isolated

echo ""
echo "================================"
echo "Deployment completed successfully!"
echo "Function App: $FUNCTION_APP_NAME"
echo "================================"
echo ""
echo "To view logs, run:"
echo "  func azure functionapp logstream $FUNCTION_APP_NAME"
