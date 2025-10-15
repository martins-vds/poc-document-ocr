# Deployment Guide - Manual

This guide walks through deploying the Document OCR Processor to Azure using manual Azure CLI commands.

> **📋 Note:** For automated deployment with Infrastructure as Code (IaC), see the [IaC Deployment Guide](DEPLOYMENT-IAC.md). The IaC approach is recommended as it provides private networking, managed identity authentication, and consistent deployments.

## Prerequisites

- Azure CLI installed
- .NET 8.0 SDK
- Azure Functions Core Tools v4
- Active Azure subscription

## Step 1: Create Azure Resources

### 1.1 Create Resource Group

```bash
az group create --name rg-document-ocr --location eastus
```

### 1.2 Create Storage Account

```bash
az storage account create \
  --name stdocumentocr \
  --resource-group rg-document-ocr \
  --location eastus \
  --sku Standard_LRS
```

### 1.3 Create Storage Containers and Queue

```bash
# Get connection string
STORAGE_CONNECTION=$(az storage account show-connection-string \
  --name stdocumentocr \
  --resource-group rg-document-ocr \
  --output tsv)

# Create containers
az storage container create --name uploaded-pdfs --connection-string "$STORAGE_CONNECTION"
az storage container create --name processed-documents --connection-string "$STORAGE_CONNECTION"

# Create queue
az storage queue create --name pdf-processing-queue --connection-string "$STORAGE_CONNECTION"
```

### 1.4 Create Document Intelligence

```bash
az cognitiveservices account create \
  --name doc-intelligence-ocr \
  --resource-group rg-document-ocr \
  --kind FormRecognizer \
  --sku S0 \
  --location eastus

# Get endpoint and key
DOC_ENDPOINT=$(az cognitiveservices account show \
  --name doc-intelligence-ocr \
  --resource-group rg-document-ocr \
  --query properties.endpoint \
  --output tsv)

DOC_KEY=$(az cognitiveservices account keys list \
  --name doc-intelligence-ocr \
  --resource-group rg-document-ocr \
  --query key1 \
  --output tsv)
```

### 1.5 Create Cosmos DB Account and Database

```bash
# Create Cosmos DB account
az cosmosdb create \
  --name cosmos-document-ocr \
  --resource-group rg-document-ocr \
  --locations regionName=eastus

# Create database
az cosmosdb sql database create \
  --account-name cosmos-document-ocr \
  --resource-group rg-document-ocr \
  --name DocumentOcrDb

# Create container with partition key
az cosmosdb sql container create \
  --account-name cosmos-document-ocr \
  --resource-group rg-document-ocr \
  --database-name DocumentOcrDb \
  --name ProcessedDocuments \
  --partition-key-path "/identifier"

# Get endpoint and key
COSMOS_ENDPOINT=$(az cosmosdb show \
  --name cosmos-document-ocr \
  --resource-group rg-document-ocr \
  --query documentEndpoint \
  --output tsv)

COSMOS_KEY=$(az cosmosdb keys list \
  --name cosmos-document-ocr \
  --resource-group rg-document-ocr \
  --query primaryMasterKey \
  --output tsv)
```

### 1.6 Create Function App

```bash
az functionapp create \
  --resource-group rg-document-ocr \
  --name func-document-ocr \
  --storage-account stdocumentocr \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4
```

## Step 2: Configure Application Settings

```bash
az functionapp config appsettings set \
  --name func-document-ocr \
  --resource-group rg-document-ocr \
  --settings \
    "DocumentIntelligence:Endpoint=$DOC_ENDPOINT" \
    "DocumentIntelligence:ApiKey=$DOC_KEY" \
    "CosmosDb:Endpoint=$COSMOS_ENDPOINT" \
    "CosmosDb:Key=$COSMOS_KEY" \
    "CosmosDb:DatabaseName=DocumentOcrDb" \
    "CosmosDb:ContainerName=ProcessedDocuments"
```

## Step 3: Deploy Function Code

```bash
cd src/DocumentOcrProcessor
func azure functionapp publish func-document-ocr
```

## Step 4: Set Up Logic App (Optional)

If you want to automate email processing:

1. Create a Logic App in the Azure Portal
2. Add an email trigger (Office 365, Gmail, etc.)
3. Add an action to save attachments to the `uploaded-pdfs` container
4. Add an action to send a message to the `pdf-processing-queue` queue

Example Logic App workflow:
```json
{
  "definition": {
    "triggers": {
      "When_a_new_email_arrives": {
        "type": "ApiConnection",
        "inputs": {
          "host": {
            "connection": {
              "name": "@parameters('$connections')['office365']['connectionId']"
            }
          },
          "method": "get",
          "path": "/Mail/OnNewEmail"
        }
      }
    },
    "actions": {
      "For_each_attachment": {
        "foreach": "@triggerBody()?['attachments']",
        "actions": {
          "Upload_to_blob": {
            "type": "ApiConnection",
            "inputs": {
              "host": {
                "connection": {
                  "name": "@parameters('$connections')['azureblob']['connectionId']"
                }
              },
              "method": "post",
              "path": "/datasets/default/files",
              "body": "@items('For_each_attachment')?['contentBytes']"
            }
          },
          "Send_queue_message": {
            "type": "ApiConnection",
            "inputs": {
              "host": {
                "connection": {
                  "name": "@parameters('$connections')['azurequeues']['connectionId']"
                }
              },
              "method": "post",
              "path": "/queues/pdf-processing-queue/messages",
              "body": {
                "BlobName": "@{items('For_each_attachment')?['name']}",
                "ContainerName": "uploaded-pdfs"
              }
            }
          }
        }
      }
    }
  }
}
```

## Step 5: Test the Deployment

### Manual Test

```bash
# Upload a test PDF
az storage blob upload \
  --account-name stdocumentocr \
  --container-name uploaded-pdfs \
  --name test.pdf \
  --file /path/to/test.pdf \
  --connection-string "$STORAGE_CONNECTION"

# Send queue message
az storage message put \
  --queue-name pdf-processing-queue \
  --content '{"BlobName":"test.pdf","ContainerName":"uploaded-pdfs"}' \
  --connection-string "$STORAGE_CONNECTION"

# Check logs
az functionapp log tail --name func-document-ocr --resource-group rg-document-ocr

# Check results
az storage blob list \
  --account-name stdocumentocr \
  --container-name processed-documents \
  --connection-string "$STORAGE_CONNECTION" \
  --output table
```

## Step 6: Monitor

Enable Application Insights for monitoring:

```bash
az monitor app-insights component create \
  --app func-document-ocr \
  --location eastus \
  --resource-group rg-document-ocr

APPINSIGHTS_KEY=$(az monitor app-insights component show \
  --app func-document-ocr \
  --resource-group rg-document-ocr \
  --query instrumentationKey \
  --output tsv)

az functionapp config appsettings set \
  --name func-document-ocr \
  --resource-group rg-document-ocr \
  --settings "APPINSIGHTS_INSTRUMENTATIONKEY=$APPINSIGHTS_KEY"
```

## Cleanup

To remove all resources:

```bash
az group delete --name rg-document-ocr --yes --no-wait
```

## Troubleshooting

### Function not triggering
- Check queue message format is valid JSON
- Verify connection string is correct
- Check function logs for errors

### Document Intelligence errors
- Verify endpoint and API key are correct
- Check service quota limits
- Ensure PDF is valid and not corrupted
