# Quick Start Guide

This guide will help you get the Document OCR Processor running locally for development.

## Prerequisites

- .NET 8.0 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Azure Functions Core Tools v4 ([Install Guide](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local))
- Azure Storage Emulator or Azurite ([Install Guide](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite))
- Active Azure subscription for AI services

## Setup

### 1. Clone the Repository

```bash
git clone https://github.com/martins-vds/poc-document-ocr.git
cd poc-document-ocr/src/DocumentOcrProcessor
```

### 2. Configure Local Settings

Copy the template and fill in your Azure service credentials:

```bash
cp local.settings.json.template local.settings.json
```

Edit `local.settings.json` and replace the placeholders:

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "DocumentIntelligence:Endpoint": "https://YOUR-RESOURCE.cognitiveservices.azure.com/",
        "DocumentIntelligence:ApiKey": "YOUR-API-KEY",
        "CosmosDb:Endpoint": "https://YOUR-COSMOSDB-ACCOUNT.documents.azure.com:443/",
        "CosmosDb:Key": "YOUR-COSMOSDB-KEY",
        "CosmosDb:DatabaseName": "DocumentOcrDb",
        "CosmosDb:ContainerName": "ProcessedDocuments"
    }
}
```

### 3. Start Azure Storage Emulator

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

### 4. Create Storage Queue and Containers

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

### 5. Build and Run

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

1. Open `src/DocumentOcrProcessor/DocumentOcrProcessor.csproj` in Visual Studio
2. Set breakpoints in the code
3. Press F5 to start debugging

### Visual Studio Code

1. Open the `src/DocumentOcrProcessor` folder in VS Code
2. Install the Azure Functions extension
3. Press F5 to start debugging
4. Set breakpoints as needed

### Common Issues

**Queue trigger not firing**:
- Verify Azurite/Storage Emulator is running
- Check connection string in `local.settings.json`
- Ensure queue exists and has the correct name

**Document Intelligence errors**:
- Verify endpoint URL ends with `/`
- Check API key is valid
- Ensure service is in the same region or has global access

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
cd ../../ && dotnet build src/DocumentOcrProcessor && cd tests && dotnet test

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
