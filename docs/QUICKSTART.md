# Quick Start Guide

This guide will help you get the Document OCR Processor running locally for development using **keyless authentication** with DefaultAzureCredential.

> **🔐 Security:** This project uses keyless authentication - no API keys or connection strings in configuration!

## Prerequisites

- .NET 8.0 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Azure Functions Core Tools v4 ([Install Guide](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local))
- Azure Storage Emulator or Azurite ([Install Guide](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite))
- Azure CLI ([Install Guide](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli))
- Active Azure subscription for AI services
- **Azure credentials configured** (see Authentication Setup below)

## Setup

### 1. Clone the Repository

```bash
git clone https://github.com/martins-vds/poc-document-ocr.git
cd poc-document-ocr/src/DocumentOcr.Processor
```

### 2. Configure Azure Authentication (Required for Keyless Auth)

DefaultAzureCredential will attempt authentication in the following order:

**Option A: Azure CLI (Recommended for local development)**
```bash
az login
# Set your subscription if you have multiple
az account set --subscription "Your-Subscription-Name"
```

**Option B: Environment Variables**
```bash
export AZURE_TENANT_ID="your-tenant-id"
export AZURE_CLIENT_ID="your-client-id"
export AZURE_CLIENT_SECRET="your-client-secret"
```

**Option C: Visual Studio / VS Code**
- Sign in to Azure through the IDE
- DefaultAzureCredential will use those credentials

### 3. Configure Local Settings

**Option A: Use azd Provision (Recommended for Azure deployments)**

If you've deployed infrastructure using Azure Developer CLI:

```bash
# The configuration is automatically set up after azd provision
azd provision
```

The postprovision hook automatically:
1. Retrieves keys and connection strings from Azure
2. Updates local configuration files for both Function App and Web App
3. You're ready to develop locally!

**Option B: Use the Configuration Utility Script (Manual setup)**

Use the utility script to manually update both Function App and Web App settings (keyless mode):

```bash
# From the project root
cd utils
python update_settings.py --interactive
```

This will prompt you for:
- Storage account name (e.g., `devstoreaccount1` for local, or your Azure storage account name)
- Document Intelligence endpoint
- Cosmos DB endpoint
- Azure AD configuration (for web app)

**No API keys or connection strings needed!**

For local development with emulators:

```bash
python update_settings.py \
  --storage-account "devstoreaccount1" \
  --doc-intelligence-endpoint "https://YOUR-RESOURCE.cognitiveservices.azure.com/" \
  --cosmosdb-endpoint "https://localhost:8081" \
  --tenant-id "common" \
  --client-id "your-dev-client-id" \
  --domain "localhost"
```

See [`utils/README.md`](../utils/README.md) for more examples and options.

**Option C: Manual Configuration**

Copy the template and fill in your Azure service configuration:

```bash
cp local.settings.json.template local.settings.json
```

Edit `local.settings.json` and replace the placeholders (no keys needed):

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage__accountName": "devstoreaccount1",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "Storage:AccountName": "devstoreaccount1",
        "DocumentIntelligence:Endpoint": "https://YOUR-RESOURCE.cognitiveservices.azure.com/",
        "CosmosDb:Endpoint": "https://YOUR-COSMOSDB-ACCOUNT.documents.azure.com:443/",
        "CosmosDb:DatabaseName": "DocumentOcrDb",
        "CosmosDb:ContainerName": "ProcessedDocuments"
    }
}
```

**Important:** Your Azure CLI credentials will be used for authentication!

### 4. Assign Local Development Permissions

For local development, your Azure user needs permissions on Azure resources:

```bash
# Get your user's object ID
USER_ID=$(az ad signed-in-user show --query id --output tsv)

# Assign Storage permissions (if using Azure Storage instead of emulator)
az role assignment create \
  --assignee $USER_ID \
  --role "Storage Blob Data Contributor" \
  --scope "/subscriptions/YOUR-SUB-ID/resourceGroups/YOUR-RG/providers/Microsoft.Storage/storageAccounts/YOUR-STORAGE"

# Assign Document Intelligence permissions
az role assignment create \
  --assignee $USER_ID \
  --role "Cognitive Services User" \
  --scope "/subscriptions/YOUR-SUB-ID/resourceGroups/YOUR-RG/providers/Microsoft.CognitiveServices/accounts/YOUR-DOC-INTEL"

# Assign Cosmos DB permissions
az cosmosdb sql role assignment create \
  --account-name YOUR-COSMOS-ACCOUNT \
  --resource-group YOUR-RG \
  --role-definition-name "Cosmos DB Built-in Data Contributor" \
  --principal-id $USER_ID \
  --scope "/subscriptions/YOUR-SUB-ID/resourceGroups/YOUR-RG/providers/Microsoft.DocumentDB/databaseAccounts/YOUR-COSMOS-ACCOUNT"
```

### 5. Start Azure Storage Emulator

**Option A: Using Azurite (Recommended)**

```bash
# Install Azurite globally
npm install -g azurite

# Start Azurite
azurite --silent --location /tmp/azurite --debug /tmp/azurite/debug.log
```

**Option B: Using Azure Storage Emulator (Windows only)**

Start the Azure Storage Emulator from the Start menu or run:
```cmd
AzureStorageEmulator.exe start
```

### 6. Create Storage Queue and Containers

Using Azure Storage Explorer or Azure CLI:

```bash
# Using Azure CLI with local emulator
CONNECTION_STRING="UseDevelopmentStorage=true"

# Create queue
az storage queue create \
  --name pdf-processing-queue \
  --connection-string "$CONNECTION_STRING"

# Create containers
az storage container create \
  --name uploaded-pdfs \
  --connection-string "$CONNECTION_STRING"

az storage container create \
  --name processed-documents \
  --connection-string "$CONNECTION_STRING"
```

Or using PowerShell with Azure Storage Emulator:

```powershell
# Install Azure.Storage PowerShell module if not already installed
Install-Module -Name Az.Storage

# Create queue and containers
$ctx = New-AzStorageContext -Local
New-AzStorageQueue -Name "pdf-processing-queue" -Context $ctx
New-AzStorageContainer -Name "uploaded-pdfs" -Context $ctx
New-AzStorageContainer -Name "processed-documents" -Context $ctx
```

### 7. Build and Run

```bash
# Restore packages
dotnet restore

# Build the project
dotnet build

# Start the function locally
func start
```

You should see output indicating the function is running:

```
Azure Functions Core Tools
Core Tools Version:       4.x.x
Function Runtime Version: 4.x.x

Functions:

        PdfProcessorFunction: queueTrigger

For detailed output, run func with --verbose flag.
```

## Testing Locally

### Manual Test

1. **Upload a PDF to the local storage**:

```bash
# Using Azure CLI
az storage blob upload \
  --account-name devstoreaccount1 \
  --use-emulator \
  --container-name uploaded-pdfs \
  --name test.pdf \
  --file /path/to/your/test.pdf
```

2. **Send a message to the queue**:

```bash
# Using Azure CLI
az storage message put \
  --account-name devstoreaccount1 \
  --use-emulator \
  --queue-name pdf-processing-queue \
  --content '{"BlobName":"test.pdf","ContainerName":"uploaded-pdfs"}'

# With custom identifier field name
az storage message put \
  --account-name devstoreaccount1 \
  --use-emulator \
  --queue-name pdf-processing-queue \
  --content '{"BlobName":"test.pdf","ContainerName":"uploaded-pdfs","IdentifierFieldName":"documentId"}'
```

Or using PowerShell:

```powershell
# Upload blob
$ctx = New-AzStorageContext -Local
Set-AzStorageBlobContent -File "C:\path\to\test.pdf" `
  -Container "uploaded-pdfs" `
  -Blob "test.pdf" `
  -Context $ctx

# Send queue message
$queue = Get-AzStorageQueue -Name "pdf-processing-queue" -Context $ctx
$queueMessage = '{"BlobName":"test.pdf","ContainerName":"uploaded-pdfs"}'
$queue.CloudQueue.AddMessageAsync([Microsoft.Azure.Storage.Queue.CloudQueueMessage]::new($queueMessage))
```

3. **Watch the function logs**:

The function will automatically process the message. You should see logs in the console indicating:
- PDF download
- PDF page to image conversion
- Document Intelligence analysis (OCR)
- Page aggregation by identifier field
- PDF creation from aggregated pages
- Results upload to blob storage and Cosmos DB

4. **Check the results**:

```bash
# List processed documents
az storage blob list \
  --account-name devstoreaccount1 \
  --use-emulator \
  --container-name processed-documents \
  --output table

# Download result JSON
az storage blob download \
  --account-name devstoreaccount1 \
  --use-emulator \
  --container-name processed-documents \
  --name test_result.json \
  --file result.json

# View the result
cat result.json
```

## Debugging

### Visual Studio

1. Open `src/DocumentOcr.Processor/DocumentOcr.Processor.csproj` in Visual Studio
2. Set breakpoints in the code
3. Press F5 to start debugging

### Visual Studio Code

1. Open the `src/DocumentOcr.Processor` folder in VS Code
2. Install the Azure Functions extension
3. Press F5 to start debugging
4. Set breakpoints as needed

### Common Issues

**Queue trigger not firing**:
- Verify Azurite/Storage Emulator is running
- Check configuration in `local.settings.json`
- Ensure queue exists and has the correct name
- Verify Azure credentials are configured (run `az account show`)

**Document Intelligence errors**:
- Verify endpoint URL ends with `/`
- Ensure you're logged in with Azure CLI (`az login`)
- Check that your user has "Cognitive Services User" role
- Ensure service is accessible from your location

**Cosmos DB errors**:
- Verify endpoint format
- Ensure you're logged in with Azure CLI
- Check that your user has Cosmos DB data access role
- Verify database and container exist

**Authentication errors**:
- Run `az account show` to verify you're logged in
- Check that you have the required role assignments
- Wait a few minutes for role assignments to propagate
- Try `az account get-access-token --resource https://storage.azure.com/` to test token acquisition

## Authentication Deep Dive

The application uses `DefaultAzureCredential` which attempts to authenticate through (in order):

1. **Environment variables** - `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`
2. **Managed identity** - When running in Azure
3. **Azure CLI** - Your `az login` credentials (best for local dev)
4. **Azure PowerShell** - Your PowerShell Azure credentials
5. **Visual Studio** - VS authentication
6. **VS Code** - VS Code Azure Account extension

For local development, we recommend using Azure CLI (`az login`).

## Next Steps

- See [ARCHITECTURE.md](ARCHITECTURE.md) for system design details
- See [DEPLOYMENT.md](DEPLOYMENT.md) for Azure deployment instructions
- Check the main [README.md](../README.md) for complete documentation

## Useful Commands

```bash
# Clean build
dotnet clean && dotnet build

# Run tests
cd ../../tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Build and test in one command (from repository root)
cd ../../ && dotnet build src/DocumentOcr.Processor && cd tests && dotnet test

# View function help
func --help

# Check function version
func --version

# List all functions in the project
func list
```

## Tips for Development

1. **Use verbose logging**: Set `FUNCTIONS_WORKER_RUNTIME_VERSION` to see detailed logs
2. **Monitor storage**: Use Azure Storage Explorer to inspect queues, blobs, and messages
3. **Test incrementally**: Test each service separately before running the full pipeline
4. **Use sample PDFs**: Start with simple, small PDFs before testing complex documents
5. **Check quotas**: Be aware of API rate limits for AI services during development
