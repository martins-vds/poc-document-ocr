#!/bin/bash
set -e

echo ""
echo "================================================================"
echo "Post-Provision Hook: Setting up keyless authentication config"
echo "================================================================"
echo ""

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Check if required environment variables are set by azd
if [ -z "$AZURE_RESOURCE_GROUP" ]; then
    echo "Warning: AZURE_RESOURCE_GROUP not set. Skipping local configuration setup."
    echo "This is expected if running outside of 'azd provision'."
    exit 0
fi

# Retrieving Azure resource configuration for keyless authentication...
echo ""

# Get resource names from azd environment
STORAGE_ACCOUNT_NAME="${AZURE_STORAGE_ACCOUNT_NAME:-}"
DOC_INTELLIGENCE_NAME="${AZURE_DOCUMENTINTELLIGENCE_NAME:-}"
COSMOSDB_ACCOUNT_NAME="${AZURE_COSMOSDB_ACCOUNT_NAME:-}"

# If not set, try to get from bicep outputs via azd
if [ -z "$STORAGE_ACCOUNT_NAME" ]; then
    STORAGE_ACCOUNT_NAME=$(azd env get-value AZURE_STORAGE_ACCOUNT_NAME 2>/dev/null || echo "")
fi
if [ -z "$DOC_INTELLIGENCE_NAME" ]; then
    DOC_INTELLIGENCE_NAME=$(azd env get-value AZURE_DOCUMENTINTELLIGENCE_NAME 2>/dev/null || echo "")
fi
if [ -z "$COSMOSDB_ACCOUNT_NAME" ]; then
    COSMOSDB_ACCOUNT_NAME=$(azd env get-value AZURE_COSMOSDB_ACCOUNT_NAME 2>/dev/null || echo "")
fi

# Get additional values from azd outputs
DOC_INTELLIGENCE_ENDPOINT=$(azd env get-value AZURE_DOCUMENTINTELLIGENCE_ENDPOINT 2>/dev/null || echo "")
COSMOSDB_ENDPOINT=$(azd env get-value AZURE_COSMOSDB_ENDPOINT 2>/dev/null || echo "")
TENANT_ID=$(azd env get-value AZURE_TENANT_ID 2>/dev/null || echo "")
WEB_APP_CLIENT_ID=$(azd env get-value AZURE_WEB_APP_CLIENT_ID 2>/dev/null || echo "")
AZURE_AD_DOMAIN=$(azd env get-value AZURE_AD_DOMAIN 2>/dev/null || echo "")

echo "Resource Group: $AZURE_RESOURCE_GROUP"
echo "Storage Account: $STORAGE_ACCOUNT_NAME"
echo "Document Intelligence: $DOC_INTELLIGENCE_NAME"
echo "Cosmos DB: $COSMOSDB_ACCOUNT_NAME"
echo ""

# Set environment variables for keyless authentication (no keys needed)
echo "Setting up environment variables for keyless authentication..."

if [ -n "$STORAGE_ACCOUNT_NAME" ]; then
    export AZURE_STORAGE_ACCOUNT_NAME="$STORAGE_ACCOUNT_NAME"
    echo "✓ Storage account name set"
fi

if [ -n "$DOC_INTELLIGENCE_ENDPOINT" ]; then
    export AZURE_DOCUMENTINTELLIGENCE_ENDPOINT="$DOC_INTELLIGENCE_ENDPOINT"
    echo "✓ Document Intelligence endpoint set"
fi

if [ -n "$COSMOSDB_ENDPOINT" ]; then
    export AZURE_COSMOSDB_ENDPOINT="$COSMOSDB_ENDPOINT"
    export AZURE_COSMOSDB_DATABASE="DocumentOcrDb"
    export AZURE_COSMOSDB_CONTAINER="ProcessedDocuments"
    echo "✓ Cosmos DB configuration set"
fi

if [ -n "$TENANT_ID" ]; then
    export AZURE_TENANT_ID="$TENANT_ID"
    echo "✓ Azure AD tenant ID set"
fi

if [ -n "$WEB_APP_CLIENT_ID" ]; then
    export AZURE_WEB_APP_CLIENT_ID="$WEB_APP_CLIENT_ID"
    echo "✓ Web App client ID set"
fi

if [ -n "$AZURE_AD_DOMAIN" ]; then
    export AZURE_AD_DOMAIN="$AZURE_AD_DOMAIN"
    echo "✓ Azure AD domain set"
fi

# Check if we have all required values for keyless authentication
missing_vars=()
[ -z "$AZURE_STORAGE_ACCOUNT_NAME" ] && missing_vars+=("AZURE_STORAGE_ACCOUNT_NAME")
[ -z "$AZURE_DOCUMENTINTELLIGENCE_ENDPOINT" ] && missing_vars+=("AZURE_DOCUMENTINTELLIGENCE_ENDPOINT")
[ -z "$AZURE_COSMOSDB_ENDPOINT" ] && missing_vars+=("AZURE_COSMOSDB_ENDPOINT")
[ -z "$AZURE_TENANT_ID" ] && missing_vars+=("AZURE_TENANT_ID")
[ -z "$AZURE_WEB_APP_CLIENT_ID" ] && missing_vars+=("AZURE_WEB_APP_CLIENT_ID")
[ -z "$AZURE_AD_DOMAIN" ] && missing_vars+=("AZURE_AD_DOMAIN")

if [ ${#missing_vars[@]} -gt 0 ]; then
    echo ""
    echo "⚠ Warning: Missing required environment variables for keyless authentication:"
    printf "   - %s\n" "${missing_vars[@]}"
    echo "Local configuration files will not be updated."
    echo "You can update them manually using: python utils/update_settings.py --interactive"
    exit 0
fi

# Update local configuration files using the utility script
echo ""
echo "Updating local configuration files..."
echo ""

cd "$PROJECT_ROOT"

if [ -f "utils/update_settings.py" ]; then
    python3 utils/update_settings.py --from-azd-env
    echo ""
    echo "✓ Local configuration files updated successfully!"
    echo ""
    echo "You can now run the applications locally:"
    echo "  - Function App: cd src/DocumentOcrProcessor && func start"
    echo "  - Web App: cd src/DocumentOcrWebApp && dotnet run"
else
    echo "⚠ Warning: utils/update_settings.py not found"
    echo "Skipping local configuration update"
fi

echo ""
echo "================================================================"
echo "Keyless authentication configuration complete!"
echo "================================================================"
echo ""
