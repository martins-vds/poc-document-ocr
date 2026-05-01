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
DOC_INTELLIGENCE_MODEL_ID=$(azd env get-value AZURE_DOCUMENTINTELLIGENCE_MODEL_ID 2>/dev/null || echo "")
IDENTIFIER_FIELD_NAME=$(azd env get-value AZURE_DOCUMENTPROCESSING_IDENTIFIER_FIELD_NAME 2>/dev/null || echo "")
COSMOSDB_ENDPOINT=$(azd env get-value AZURE_COSMOSDB_ENDPOINT 2>/dev/null || echo "")
TENANT_ID=$(azd env get-value AZURE_TENANT_ID 2>/dev/null || echo "")
WEB_APP_CLIENT_ID=$(azd env get-value AZURE_WEB_APP_CLIENT_ID 2>/dev/null || echo "")
AZURE_AD_DOMAIN=$(azd env get-value AZURE_AD_DOMAIN 2>/dev/null || echo "")
FUNCTION_APP_URL=$(azd env get-value AZURE_FUNCTION_APP_URL 2>/dev/null || echo "")

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

if [ -n "$DOC_INTELLIGENCE_MODEL_ID" ]; then
    export AZURE_DOCUMENTINTELLIGENCE_MODEL_ID="$DOC_INTELLIGENCE_MODEL_ID"
    echo "✓ Document Intelligence model ID set ($DOC_INTELLIGENCE_MODEL_ID)"
fi

if [ -n "$IDENTIFIER_FIELD_NAME" ]; then
    export AZURE_DOCUMENTPROCESSING_IDENTIFIER_FIELD_NAME="$IDENTIFIER_FIELD_NAME"
    echo "✓ Document processing identifier field name set ($IDENTIFIER_FIELD_NAME)"
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

if [ -n "$FUNCTION_APP_URL" ]; then
    # Format as full URL with https://
    if [[ ! "$FUNCTION_APP_URL" =~ ^http ]]; then
        FUNCTION_APP_URL="https://$FUNCTION_APP_URL"
    fi
    export AZURE_OPERATIONS_API_URL="$FUNCTION_APP_URL"
    echo "✓ Operations API URL set"
fi

# Note: AZURE_OPERATIONS_API_KEY is optional and typically not set in local development
# It can be retrieved from Azure Portal or Azure CLI if needed
export AZURE_OPERATIONS_API_KEY=""

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
    echo "  - Function App: cd src/DocumentOcr.Processor && func start"
    echo "  - Web App: cd src/DocumentOcr.WebApp && dotnet run"
else
    echo "⚠ Warning: utils/update_settings.py not found"
    echo "Skipping local configuration update"
fi

echo ""
echo "================================================================"
echo "Keyless authentication configuration complete!"
echo "================================================================"
echo ""

# ----------------------------------------------------------------------
# FR-010 — legacy-record wipe guard (feature 001-document-schema-aggregation)
# ----------------------------------------------------------------------
# Detect legacy Cosmos records that lack the new `schema` property
# (pre-001-document-schema-aggregation shape). These are incompatible
# with the rewritten Review page and the schema-driven mapper, so they
# MUST be removed before the new code path overwrites them.
#
# Destructive: the wipe deletes ALL items in the
# DocumentOcrDb.ProcessedDocuments container. Require explicit opt-in
# via CONFIRM_WIPE_DOCUMENTS=yes. If a legacy record is detected and
# the env var is unset, exit non-zero with a loud message so the
# operator knows action is required.

if [ -n "$COSMOSDB_ACCOUNT_NAME" ] && command -v az >/dev/null 2>&1; then
    echo "FR-010: scanning Cosmos container for legacy records..."
    LEGACY_QUERY='SELECT VALUE COUNT(1) FROM c WHERE NOT IS_DEFINED(c.schema)'
    LEGACY_COUNT=$(az cosmosdb sql query \
        --account-name "$COSMOSDB_ACCOUNT_NAME" \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --database-name "DocumentOcrDb" \
        --container-name "ProcessedDocuments" \
        --query-text "$LEGACY_QUERY" \
        --query "[0]" -o tsv 2>/dev/null || echo "0")

    if [ "$LEGACY_COUNT" -gt 0 ] 2>/dev/null; then
        if [ "${CONFIRM_WIPE_DOCUMENTS:-no}" = "yes" ]; then
            echo "⚠ Wiping $LEGACY_COUNT legacy record(s) per CONFIRM_WIPE_DOCUMENTS=yes..."
            az cosmosdb sql container delete \
                --account-name "$COSMOSDB_ACCOUNT_NAME" \
                --resource-group "$AZURE_RESOURCE_GROUP" \
                --database-name "DocumentOcrDb" \
                --name "ProcessedDocuments" \
                --yes >/dev/null
            az cosmosdb sql container create \
                --account-name "$COSMOSDB_ACCOUNT_NAME" \
                --resource-group "$AZURE_RESOURCE_GROUP" \
                --database-name "DocumentOcrDb" \
                --name "ProcessedDocuments" \
                --partition-key-path "/identifier" >/dev/null
            echo "✓ Container recreated with partition key /identifier."
        else
            echo ""
            echo "================================================================"
            echo "❌ FR-010: $LEGACY_COUNT legacy record(s) without 'schema' detected"
            echo "================================================================"
            echo "These records predate feature 001-document-schema-aggregation and"
            echo "are incompatible with the current code. The Review page WILL"
            echo "misrender them and the processor's duplicate-skip pre-check WILL"
            echo "preserve them indefinitely."
            echo ""
            echo "To wipe and recreate the ProcessedDocuments container, re-run:"
            echo "  CONFIRM_WIPE_DOCUMENTS=yes azd hooks run postprovision"
            echo ""
            exit 1
        fi
    else
        echo "✓ No legacy records detected."
    fi
fi

