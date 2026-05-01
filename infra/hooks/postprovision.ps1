#!/usr/bin/env pwsh

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Post-Provision Hook: Setting up keyless authentication config" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Get the directory where this script is located
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)

# Check if required environment variables are set by azd
if (-not $env:AZURE_RESOURCE_GROUP) {
    Write-Host "Warning: AZURE_RESOURCE_GROUP not set. Skipping local configuration setup." -ForegroundColor Yellow
    Write-Host "This is expected if running outside of 'azd provision'." -ForegroundColor Yellow
    exit 0
}

Write-Host "Retrieving Azure resource configuration for keyless authentication..."
Write-Host ""

# Get resource names from azd environment
$StorageAccountName = $env:AZURE_STORAGE_ACCOUNT_NAME
$DocIntelligenceName = $env:AZURE_DOCUMENTINTELLIGENCE_NAME
$CosmosDbAccountName = $env:AZURE_COSMOSDB_ACCOUNT_NAME

# If not set, try to get from bicep outputs via azd
if (-not $StorageAccountName) {
    $StorageAccountName = (azd env get-value AZURE_STORAGE_ACCOUNT_NAME 2>$null) | Out-String
    $StorageAccountName = $StorageAccountName.Trim()
}
if (-not $DocIntelligenceName) {
    $DocIntelligenceName = (azd env get-value AZURE_DOCUMENTINTELLIGENCE_NAME 2>$null) | Out-String
    $DocIntelligenceName = $DocIntelligenceName.Trim()
}
if (-not $CosmosDbAccountName) {
    $CosmosDbAccountName = (azd env get-value AZURE_COSMOSDB_ACCOUNT_NAME 2>$null) | Out-String
    $CosmosDbAccountName = $CosmosDbAccountName.Trim()
}

# Get additional values from azd outputs
$DocIntelligenceEndpoint = (azd env get-value AZURE_DOCUMENTINTELLIGENCE_ENDPOINT 2>$null) | Out-String
$DocIntelligenceEndpoint = $DocIntelligenceEndpoint.Trim()
$DocIntelligenceModelId = (azd env get-value AZURE_DOCUMENTINTELLIGENCE_MODEL_ID 2>$null) | Out-String
$DocIntelligenceModelId = $DocIntelligenceModelId.Trim()
$IdentifierFieldName = (azd env get-value AZURE_DOCUMENTPROCESSING_IDENTIFIER_FIELD_NAME 2>$null) | Out-String
$IdentifierFieldName = $IdentifierFieldName.Trim()
$CosmosDbEndpoint = (azd env get-value AZURE_COSMOSDB_ENDPOINT 2>$null) | Out-String
$CosmosDbEndpoint = $CosmosDbEndpoint.Trim()
$TenantId = (azd env get-value AZURE_TENANT_ID 2>$null) | Out-String
$TenantId = $TenantId.Trim()
$WebAppClientId = (azd env get-value AZURE_WEB_APP_CLIENT_ID 2>$null) | Out-String
$WebAppClientId = $WebAppClientId.Trim()
$AzureAdDomain = (azd env get-value AZURE_AD_DOMAIN 2>$null) | Out-String
$AzureAdDomain = $AzureAdDomain.Trim()
$FunctionAppUrl = (azd env get-value AZURE_FUNCTION_APP_URL 2>$null) | Out-String
$FunctionAppUrl = $FunctionAppUrl.Trim()

Write-Host "Resource Group: $env:AZURE_RESOURCE_GROUP"
Write-Host "Storage Account: $StorageAccountName"
Write-Host "Document Intelligence: $DocIntelligenceName"
Write-Host "Cosmos DB: $CosmosDbAccountName"
Write-Host ""

# Set environment variables for keyless authentication (no keys needed)
Write-Host "Setting up environment variables for keyless authentication..."

if ($StorageAccountName) {
    $env:AZURE_STORAGE_ACCOUNT_NAME = $StorageAccountName
    Write-Host "✓ Storage account name set" -ForegroundColor Green
}

if ($DocIntelligenceEndpoint) {
    $env:AZURE_DOCUMENTINTELLIGENCE_ENDPOINT = $DocIntelligenceEndpoint
    Write-Host "✓ Document Intelligence endpoint set" -ForegroundColor Green
}

if ($DocIntelligenceModelId) {
    $env:AZURE_DOCUMENTINTELLIGENCE_MODEL_ID = $DocIntelligenceModelId
    Write-Host "✓ Document Intelligence model ID set ($DocIntelligenceModelId)" -ForegroundColor Green
}

if ($IdentifierFieldName) {
    $env:AZURE_DOCUMENTPROCESSING_IDENTIFIER_FIELD_NAME = $IdentifierFieldName
    Write-Host "✓ Document processing identifier field name set ($IdentifierFieldName)" -ForegroundColor Green
}

if ($CosmosDbEndpoint) {
    $env:AZURE_COSMOSDB_ENDPOINT = $CosmosDbEndpoint
    $env:AZURE_COSMOSDB_DATABASE = "DocumentOcrDb"
    $env:AZURE_COSMOSDB_CONTAINER = "ProcessedDocuments"
    Write-Host "✓ Cosmos DB configuration set" -ForegroundColor Green
}

if ($TenantId) {
    $env:AZURE_TENANT_ID = $TenantId
    Write-Host "✓ Azure AD tenant ID set" -ForegroundColor Green
}

if ($WebAppClientId) {
    $env:AZURE_WEB_APP_CLIENT_ID = $WebAppClientId
    Write-Host "✓ Web App client ID set" -ForegroundColor Green
}

if ($AzureAdDomain) {
    $env:AZURE_AD_DOMAIN = $AzureAdDomain
    Write-Host "✓ Azure AD domain set" -ForegroundColor Green
}

if ($FunctionAppUrl) {
    # Format as full URL with https://
    if (-not $FunctionAppUrl.StartsWith("http")) {
        $FunctionAppUrl = "https://$FunctionAppUrl"
    }
    $env:AZURE_OPERATIONS_API_URL = $FunctionAppUrl
    Write-Host "✓ Operations API URL set" -ForegroundColor Green
}

# Note: AZURE_OPERATIONS_API_KEY is optional and typically not set in local development
# It can be retrieved from Azure Portal or Azure CLI if needed
$env:AZURE_OPERATIONS_API_KEY = ""

# Check if we have all required values for keyless authentication
$MissingVars = @()
if (-not $env:AZURE_STORAGE_ACCOUNT_NAME) { $MissingVars += "AZURE_STORAGE_ACCOUNT_NAME" }
if (-not $env:AZURE_DOCUMENTINTELLIGENCE_ENDPOINT) { $MissingVars += "AZURE_DOCUMENTINTELLIGENCE_ENDPOINT" }
if (-not $env:AZURE_COSMOSDB_ENDPOINT) { $MissingVars += "AZURE_COSMOSDB_ENDPOINT" }
if (-not $env:AZURE_TENANT_ID) { $MissingVars += "AZURE_TENANT_ID" }
if (-not $env:AZURE_WEB_APP_CLIENT_ID) { $MissingVars += "AZURE_WEB_APP_CLIENT_ID" }
if (-not $env:AZURE_AD_DOMAIN) { $MissingVars += "AZURE_AD_DOMAIN" }

if ($MissingVars.Count -gt 0) {
    Write-Host ""
    Write-Host "⚠ Warning: Missing required environment variables for keyless authentication:" -ForegroundColor Yellow
    foreach ($var in $MissingVars) {
        Write-Host "   - $var" -ForegroundColor Yellow
    }
    Write-Host "Local configuration files will not be updated." -ForegroundColor Yellow
    Write-Host "You can update them manually using: python utils/update_settings.py --interactive" -ForegroundColor Yellow
    exit 0
}

# Update local configuration files using the utility script
Write-Host ""
Write-Host "Updating local configuration files..."
Write-Host ""

Set-Location $ProjectRoot

if (Test-Path "utils/update_settings.py") {
    python utils/update_settings.py --from-azd-env
    Write-Host ""
    Write-Host "✓ Local configuration files updated successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now run the applications locally:"
    Write-Host "  - Function App: cd src/DocumentOcr.Processor && func start"
    Write-Host "  - Web App: cd src/DocumentOcr.WebApp && dotnet run"
}
else {
    Write-Host "⚠ Warning: utils/update_settings.py not found" -ForegroundColor Yellow
    Write-Host "Skipping local configuration update" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Keyless authentication configuration complete!" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# ----------------------------------------------------------------------
# FR-010 — legacy-record wipe guard (feature 001-document-schema-aggregation)
# ----------------------------------------------------------------------
# Detect legacy Cosmos records that lack the new `schema` property.
# Destructive: requires CONFIRM_WIPE_DOCUMENTS=yes to perform the wipe.
# Otherwise exit 1 with a loud message.

if ($CosmosDbAccountName -and (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "FR-010: scanning Cosmos container for legacy records..."
    $legacyQuery = "SELECT VALUE COUNT(1) FROM c WHERE NOT IS_DEFINED(c.schema)"
    $legacyCount = 0
    try {
        $raw = az cosmosdb sql query `
            --account-name $CosmosDbAccountName `
            --resource-group $env:AZURE_RESOURCE_GROUP `
            --database-name "DocumentOcrDb" `
            --container-name "ProcessedDocuments" `
            --query-text $legacyQuery `
            --query "[0]" -o tsv 2>$null
        if ($raw) { $legacyCount = [int]$raw }
    }
    catch { $legacyCount = 0 }

    if ($legacyCount -gt 0) {
        if ($env:CONFIRM_WIPE_DOCUMENTS -eq "yes") {
            Write-Host "⚠ Wiping $legacyCount legacy record(s) per CONFIRM_WIPE_DOCUMENTS=yes..." -ForegroundColor Yellow
            az cosmosdb sql container delete `
                --account-name $CosmosDbAccountName `
                --resource-group $env:AZURE_RESOURCE_GROUP `
                --database-name "DocumentOcrDb" `
                --name "ProcessedDocuments" `
                --yes | Out-Null
            az cosmosdb sql container create `
                --account-name $CosmosDbAccountName `
                --resource-group $env:AZURE_RESOURCE_GROUP `
                --database-name "DocumentOcrDb" `
                --name "ProcessedDocuments" `
                --partition-key-path "/identifier" | Out-Null
            Write-Host "✓ Container recreated with partition key /identifier." -ForegroundColor Green
        }
        else {
            Write-Host ""
            Write-Host "================================================================" -ForegroundColor Red
            Write-Host "❌ FR-010: $legacyCount legacy record(s) without 'schema' detected" -ForegroundColor Red
            Write-Host "================================================================" -ForegroundColor Red
            Write-Host "These records predate feature 001-document-schema-aggregation and"
            Write-Host "are incompatible with the current code. The Review page WILL"
            Write-Host "misrender them and the processor's duplicate-skip pre-check WILL"
            Write-Host "preserve them indefinitely."
            Write-Host ""
            Write-Host "To wipe and recreate the ProcessedDocuments container, re-run with:"
            Write-Host "  `$env:CONFIRM_WIPE_DOCUMENTS='yes'; azd hooks run postprovision"
            Write-Host ""
            exit 1
        }
    }
    else {
        Write-Host "✓ No legacy records detected." -ForegroundColor Green
    }
}
