#!/usr/bin/env pwsh

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Post-Provision Hook: Setting up local development configuration" -ForegroundColor Cyan
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

Write-Host "Fetching Azure resource keys and connection strings..."
Write-Host ""

# Get resource names from azd environment
$StorageAccountName = $env:AZURE_STORAGE_ACCOUNT_NAME
$DocIntelligenceName = $env:AZURE_DOCUMENTINTELLIGENCE_NAME
$CosmosDbAccountName = $env:AZURE_COSMOSDB_ACCOUNT_NAME

# If not set, try to get from bicep outputs via azd
if (-not $StorageAccountName) {
    $StorageAccountName = (azd env get-value storageAccountName 2>$null) | Out-String
    $StorageAccountName = $StorageAccountName.Trim()
}
if (-not $DocIntelligenceName) {
    $DocIntelligenceName = (azd env get-value documentIntelligenceName 2>$null) | Out-String
    $DocIntelligenceName = $DocIntelligenceName.Trim()
}
if (-not $CosmosDbAccountName) {
    $CosmosDbAccountName = (azd env get-value cosmosDbAccountName 2>$null) | Out-String
    $CosmosDbAccountName = $CosmosDbAccountName.Trim()
}

# Get endpoints from azd
$DocIntelligenceEndpoint = (azd env get-value documentIntelligenceEndpoint 2>$null) | Out-String
$DocIntelligenceEndpoint = $DocIntelligenceEndpoint.Trim()
$CosmosDbEndpoint = (azd env get-value cosmosDbEndpoint 2>$null) | Out-String
$CosmosDbEndpoint = $CosmosDbEndpoint.Trim()

Write-Host "Resource Group: $env:AZURE_RESOURCE_GROUP"
Write-Host "Storage Account: $StorageAccountName"
Write-Host "Document Intelligence: $DocIntelligenceName"
Write-Host "Cosmos DB: $CosmosDbAccountName"
Write-Host ""

# Fetch Storage Account connection string
if ($StorageAccountName) {
    Write-Host "Fetching Storage Account connection string..."
    try {
        $StorageConnectionString = (az storage account show-connection-string `
            --name $StorageAccountName `
            --resource-group $env:AZURE_RESOURCE_GROUP `
            --query connectionString `
            --output tsv 2>$null) | Out-String
        $env:AZURE_STORAGE_CONNECTION_STRING = $StorageConnectionString.Trim()
        
        if ($env:AZURE_STORAGE_CONNECTION_STRING) {
            Write-Host "✓ Storage connection string retrieved" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "⚠ Failed to retrieve storage connection string" -ForegroundColor Yellow
    }
}

# Fetch Document Intelligence key
if ($DocIntelligenceName) {
    Write-Host "Fetching Document Intelligence key..."
    try {
        $DocIntelligenceKey = (az cognitiveservices account keys list `
            --name $DocIntelligenceName `
            --resource-group $env:AZURE_RESOURCE_GROUP `
            --query key1 `
            --output tsv 2>$null) | Out-String
        $env:AZURE_DOCUMENTINTELLIGENCE_KEY = $DocIntelligenceKey.Trim()
        
        if ($env:AZURE_DOCUMENTINTELLIGENCE_KEY) {
            Write-Host "✓ Document Intelligence key retrieved" -ForegroundColor Green
            $env:AZURE_DOCUMENTINTELLIGENCE_ENDPOINT = $DocIntelligenceEndpoint
        }
    }
    catch {
        Write-Host "⚠ Failed to retrieve Document Intelligence key" -ForegroundColor Yellow
    }
}

# Fetch Cosmos DB key
if ($CosmosDbAccountName) {
    Write-Host "Fetching Cosmos DB key..."
    try {
        $CosmosDbKey = (az cosmosdb keys list `
            --name $CosmosDbAccountName `
            --resource-group $env:AZURE_RESOURCE_GROUP `
            --query primaryMasterKey `
            --output tsv 2>$null) | Out-String
        $env:AZURE_COSMOSDB_KEY = $CosmosDbKey.Trim()
        
        if ($env:AZURE_COSMOSDB_KEY) {
            Write-Host "✓ Cosmos DB key retrieved" -ForegroundColor Green
            $env:AZURE_COSMOSDB_ENDPOINT = $CosmosDbEndpoint
            $env:AZURE_COSMOSDB_DATABASE = "DocumentOcrDb"
            $env:AZURE_COSMOSDB_CONTAINER = "ProcessedDocuments"
        }
    }
    catch {
        Write-Host "⚠ Failed to retrieve Cosmos DB key" -ForegroundColor Yellow
    }
}

# Check if we have all required values
if (-not $env:AZURE_STORAGE_CONNECTION_STRING -or `
    -not $env:AZURE_DOCUMENTINTELLIGENCE_KEY -or `
    -not $env:AZURE_COSMOSDB_KEY) {
    Write-Host ""
    Write-Host "⚠ Warning: Could not retrieve all required keys from Azure." -ForegroundColor Yellow
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
    Write-Host "  - Function App: cd src/DocumentOcrProcessor && func start"
    Write-Host "  - Web App: cd src/DocumentOcrWebApp && dotnet run"
}
else {
    Write-Host "⚠ Warning: utils/update_settings.py not found" -ForegroundColor Yellow
    Write-Host "Skipping local configuration update" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Post-provision setup complete!" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
