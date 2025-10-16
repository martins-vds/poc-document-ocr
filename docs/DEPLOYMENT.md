# Deployment Guide - Manual

This guide walks through deploying the Document OCR Processor to Azure using manual Azure CLI commands with **keyless authentication** (managed identities).

> **📋 Note:** For automated deployment with Infrastructure as Code (IaC), see the [IaC Deployment Guide](DEPLOYMENT-IAC.md). The IaC approach is recommended as it provides private networking, managed identity authentication, and consistent deployments.

> **🔐 Security:** This deployment uses **keyless authentication** with managed identities. No API keys or connection strings are stored in configuration.

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
  --functions-version 4 \
  --assign-identity [system]
```

## Step 2: Configure Managed Identities and RBAC

### 2.1 Get Function App Identity

```bash
FUNCTION_PRINCIPAL_ID=$(az functionapp identity show \
  --name func-document-ocr \
  --resource-group rg-document-ocr \
  --query principalId \
  --output tsv)
```

### 2.2 Assign Storage Permissions

```bash
# Get storage account ID
STORAGE_ID=$(az storage account show \
  --name stdocumentocr \
  --resource-group rg-document-ocr \
  --query id \
  --output tsv)

# Assign Storage Blob Data Contributor role
az role assignment create \
  --assignee $FUNCTION_PRINCIPAL_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID

# Assign Storage Queue Data Contributor role
az role assignment create \
  --assignee $FUNCTION_PRINCIPAL_ID \
  --role "Storage Queue Data Contributor" \
  --scope $STORAGE_ID
```

### 2.3 Assign Document Intelligence Permissions

```bash
# Get Document Intelligence ID
DOC_INTELLIGENCE_ID=$(az cognitiveservices account show \
  --name doc-intelligence-ocr \
  --resource-group rg-document-ocr \
  --query id \
  --output tsv)

# Assign Cognitive Services User role
az role assignment create \
  --assignee $FUNCTION_PRINCIPAL_ID \
  --role "Cognitive Services User" \
  --scope $DOC_INTELLIGENCE_ID
```

### 2.4 Assign Cosmos DB Permissions

```bash
# Get Cosmos DB account ID
COSMOS_ID=$(az cosmosdb show \
  --name cosmos-document-ocr \
  --resource-group rg-document-ocr \
  --query id \
  --output tsv)

# Assign Cosmos DB Built-in Data Contributor role
az cosmosdb sql role assignment create \
  --account-name cosmos-document-ocr \
  --resource-group rg-document-ocr \
  --role-definition-name "Cosmos DB Built-in Data Contributor" \
  --principal-id $FUNCTION_PRINCIPAL_ID \
  --scope $COSMOS_ID
```

## Step 3: Configure Application Settings

```bash
# Get endpoints (no keys needed!)
DOC_ENDPOINT=$(az cognitiveservices account show \
  --name doc-intelligence-ocr \
  --resource-group rg-document-ocr \
  --query properties.endpoint \
  --output tsv)

COSMOS_ENDPOINT=$(az cosmosdb show \
  --name cosmos-document-ocr \
  --resource-group rg-document-ocr \
  --query documentEndpoint \
  --output tsv)

# Configure Function App settings (keyless)
az functionapp config appsettings set \
  --name func-document-ocr \
  --resource-group rg-document-ocr \
  --settings \
    "AzureWebJobsStorage__accountName=stdocumentocr" \
    "Storage:AccountName=stdocumentocr" \
    "DocumentIntelligence:Endpoint=$DOC_ENDPOINT" \
    "CosmosDb:Endpoint=$COSMOS_ENDPOINT" \
    "CosmosDb:DatabaseName=DocumentOcrDb" \
    "CosmosDb:ContainerName=ProcessedDocuments"
```

**Note:** No API keys or connection strings are configured! The function app uses its managed identity to authenticate.

## Step 4: Deploy Function Code

```bash
cd src/DocumentOcrProcessor
func azure functionapp publish func-document-ocr
```

## Step 5: Set Up Logic App (Optional)

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

## Step 6: Test the Deployment

### Manual Test

```bash
# Upload a test PDF (using Azure CLI with your authenticated identity)
az storage blob upload \
  --auth-mode login \
  --account-name stdocumentocr \
  --container-name uploaded-pdfs \
  --name test.pdf \
  --file /path/to/test.pdf

# Send queue message
az storage message put \
  --auth-mode login \
  --account-name stdocumentocr \
  --queue-name pdf-processing-queue \
  --content '{"BlobName":"test.pdf","ContainerName":"uploaded-pdfs"}'

# Check logs
az functionapp log tail --name func-document-ocr --resource-group rg-document-ocr

# Check results
az storage blob list \
  --auth-mode login \
  --account-name stdocumentocr \
  --container-name processed-documents \
  --output table
```

**Note:** Using `--auth-mode login` authenticates with your Azure CLI credentials instead of connection strings.

## Step 7: Monitor

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

## Security Benefits of Keyless Authentication

✅ **No secrets in configuration** - API keys and connection strings are never stored
✅ **Managed identities** - Azure handles authentication automatically
✅ **Reduced attack surface** - No credentials to leak or rotate
✅ **Audit trail** - All access is logged through Azure AD
✅ **Zero trust** - Fine-grained RBAC permissions per resource

## Cleanup

To remove all resources:

```bash
az group delete --name rg-document-ocr --yes --no-wait
```

## Troubleshooting

### Function not triggering
- Check queue message format is valid JSON
- Verify managed identity has Storage Queue Data Contributor role
- Check function logs for errors

### Document Intelligence errors
- Verify endpoint URL is correct
- Ensure managed identity has Cognitive Services User role
- Check service quota limits

### Cosmos DB errors
- Verify endpoint format
- Ensure managed identity has Cosmos DB Built-in Data Contributor role
- Verify database and container exist

### Authentication errors
- Verify managed identity is enabled: `az functionapp identity show --name func-document-ocr --resource-group rg-document-ocr`
- Check role assignments: `az role assignment list --assignee $FUNCTION_PRINCIPAL_ID`
- Wait a few minutes for role assignments to propagate
