#!/bin/bash
set -e

echo ""
echo "================================================================"
echo "Post-Provision Hook: Setting up local development configuration"
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

echo "Fetching Azure resource keys and connection strings..."
echo ""

# Get resource names from azd environment
STORAGE_ACCOUNT_NAME="${AZURE_STORAGE_ACCOUNT_NAME:-}"
DOC_INTELLIGENCE_NAME="${AZURE_DOCUMENTINTELLIGENCE_NAME:-}"
COSMOSDB_ACCOUNT_NAME="${AZURE_COSMOSDB_ACCOUNT_NAME:-}"

# If not set, try to get from bicep outputs via azd
if [ -z "$STORAGE_ACCOUNT_NAME" ]; then
    STORAGE_ACCOUNT_NAME=$(azd env get-value storageAccountName 2>/dev/null || echo "")
fi
if [ -z "$DOC_INTELLIGENCE_NAME" ]; then
    DOC_INTELLIGENCE_NAME=$(azd env get-value documentIntelligenceName 2>/dev/null || echo "")
fi
if [ -z "$COSMOSDB_ACCOUNT_NAME" ]; then
    COSMOSDB_ACCOUNT_NAME=$(azd env get-value cosmosDbAccountName 2>/dev/null || echo "")
fi

# Get endpoints from azd or use defaults
DOC_INTELLIGENCE_ENDPOINT=$(azd env get-value documentIntelligenceEndpoint 2>/dev/null || echo "")
COSMOSDB_ENDPOINT=$(azd env get-value cosmosDbEndpoint 2>/dev/null || echo "")

echo "Resource Group: $AZURE_RESOURCE_GROUP"
echo "Storage Account: $STORAGE_ACCOUNT_NAME"
echo "Document Intelligence: $DOC_INTELLIGENCE_NAME"
echo "Cosmos DB: $COSMOSDB_ACCOUNT_NAME"
echo ""

# Fetch Storage Account connection string
if [ -n "$STORAGE_ACCOUNT_NAME" ]; then
    echo "Fetching Storage Account connection string..."
    export AZURE_STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
        --name "$STORAGE_ACCOUNT_NAME" \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --query connectionString \
        --output tsv 2>/dev/null || echo "")
    
    if [ -n "$AZURE_STORAGE_CONNECTION_STRING" ]; then
        echo "✓ Storage connection string retrieved"
    else
        echo "⚠ Failed to retrieve storage connection string"
    fi
fi

# Fetch Document Intelligence key
if [ -n "$DOC_INTELLIGENCE_NAME" ]; then
    echo "Fetching Document Intelligence key..."
    export AZURE_DOCUMENTINTELLIGENCE_KEY=$(az cognitiveservices account keys list \
        --name "$DOC_INTELLIGENCE_NAME" \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --query key1 \
        --output tsv 2>/dev/null || echo "")
    
    if [ -n "$AZURE_DOCUMENTINTELLIGENCE_KEY" ]; then
        echo "✓ Document Intelligence key retrieved"
        export AZURE_DOCUMENTINTELLIGENCE_ENDPOINT="$DOC_INTELLIGENCE_ENDPOINT"
    else
        echo "⚠ Failed to retrieve Document Intelligence key"
    fi
fi

# Fetch Cosmos DB key
if [ -n "$COSMOSDB_ACCOUNT_NAME" ]; then
    echo "Fetching Cosmos DB key..."
    export AZURE_COSMOSDB_KEY=$(az cosmosdb keys list \
        --name "$COSMOSDB_ACCOUNT_NAME" \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --query primaryMasterKey \
        --output tsv 2>/dev/null || echo "")
    
    if [ -n "$AZURE_COSMOSDB_KEY" ]; then
        echo "✓ Cosmos DB key retrieved"
        export AZURE_COSMOSDB_ENDPOINT="$COSMOSDB_ENDPOINT"
        export AZURE_COSMOSDB_DATABASE="DocumentOcrDb"
        export AZURE_COSMOSDB_CONTAINER="ProcessedDocuments"
    else
        echo "⚠ Failed to retrieve Cosmos DB key"
    fi
fi

# Check if we have all required values
if [ -z "$AZURE_STORAGE_CONNECTION_STRING" ] || \
   [ -z "$AZURE_DOCUMENTINTELLIGENCE_KEY" ] || \
   [ -z "$AZURE_COSMOSDB_KEY" ]; then
    echo ""
    echo "⚠ Warning: Could not retrieve all required keys from Azure."
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
echo "Post-provision setup complete!"
echo "================================================================"
echo ""
