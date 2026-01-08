# Operations API Deployment Guide

This guide helps you deploy and configure the Operations API for managing long-running document processing operations.

## Prerequisites

- Azure subscription
- Existing Document OCR Processor deployment
- Azure CLI or Azure Portal access

## Cosmos DB Setup

### Option 1: Using Bicep Infrastructure (Recommended)

If you're using the Bicep infrastructure templates, the Operations container is automatically created when you deploy or update your infrastructure:

```bash
# Deploy or update infrastructure
azd provision
```

The Bicep template (`infra/modules/cosmosDb.bicep`) now includes the Operations container with:
- Container name: `Operations`
- Partition key: `/id`
- Indexing policy: Optimized for operation queries

### Option 2: Manual Creation via Azure CLI

```bash
# Set your variables
COSMOS_ACCOUNT_NAME="<your-cosmos-account-name>"
RESOURCE_GROUP="<your-resource-group>"

# Create the Operations container
az cosmosdb sql container create \
  --account-name $COSMOS_ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --database-name DocumentOcrDb \
  --name Operations \
  --partition-key-path "/id" \
  --throughput 400
```

### Option 3: Manual Creation via Azure Portal

1. Navigate to your Cosmos DB account in the Azure Portal
2. Open **Data Explorer**
3. Expand the `DocumentOcrDb` database
4. Click **New Container**
5. Enter the following details:
   - **Container id**: `Operations`
   - **Partition key**: `/id`
   - **Throughput**: 400 RU/s (or use database shared throughput)
6. Click **OK**

## Function App Configuration

### Update Application Settings

Add the following setting to your Function App configuration:

**Via Azure CLI:**
```bash
FUNCTION_APP_NAME="<your-function-app-name>"
RESOURCE_GROUP="<your-resource-group>"

az functionapp config appsettings set \
  --name $FUNCTION_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings "CosmosDb__OperationsContainerName=Operations"
```

**Via Azure Portal:**
1. Navigate to your Function App
2. Go to **Configuration** → **Application settings**
3. Add new setting:
   - **Name**: `CosmosDb__OperationsContainerName`
   - **Value**: `Operations`
4. Click **Save**

### For Local Development

Update your `local.settings.json`:

```json
{
  "Values": {
    "CosmosDb:OperationsContainerName": "Operations"
  }
}
```

## Deploy Function Code

Deploy the updated Function App code with the Operations API:

```bash
# Navigate to the function app directory
cd src/DocumentOcr.Processor

# Deploy to Azure
func azure functionapp publish <your-function-app-name>
```

## Verify Deployment

### Test the API Endpoints

1. **Get the Function App URL and Key:**

```bash
FUNCTION_APP_NAME="<your-function-app-name>"
RESOURCE_GROUP="<your-resource-group>"

# Get Function App URL
FUNCTION_URL=$(az functionapp show \
  --name $FUNCTION_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "defaultHostName" -o tsv)

# Get Function Key (for HTTP triggers)
FUNCTION_KEY=$(az functionapp function keys list \
  --name $FUNCTION_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --function-name StartOperation \
  --query "default" -o tsv)
```

2. **Test Start Operation:**

```bash
curl -X POST "https://${FUNCTION_URL}/api/operations?code=${FUNCTION_KEY}" \
  -H "Content-Type: application/json" \
  -d '{
    "blobName": "test.pdf",
    "containerName": "uploaded-pdfs"
  }'
```

Expected response:
```json
{
  "operationId": "...",
  "status": "NotStarted",
  "statusQueryGetUri": "https://..."
}
```

3. **Test Get Operation Status:**

```bash
OPERATION_ID="<operation-id-from-previous-step>"

curl "https://${FUNCTION_URL}/api/operations/${OPERATION_ID}?code=${FUNCTION_KEY}"
```

## Enable Function Authorization

### Configure Function-Level Keys

For production, configure separate function keys:

```bash
# Create a new function key for the Operations API
az functionapp function keys create \
  --name $FUNCTION_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --function-name StartOperation \
  --key-name "operations-api-key"
```

### Configure Managed Identity (Recommended for Production)

Enable managed identity for secure access to Cosmos DB:

1. **Enable System-Assigned Managed Identity:**

```bash
az functionapp identity assign \
  --name $FUNCTION_APP_NAME \
  --resource-group $RESOURCE_GROUP
```

2. **Grant Cosmos DB Data Contributor Role:**

```bash
COSMOS_ACCOUNT_NAME="<your-cosmos-account-name>"
FUNCTION_PRINCIPAL_ID=$(az functionapp identity show \
  --name $FUNCTION_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

az cosmosdb sql role assignment create \
  --account-name $COSMOS_ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --scope "/" \
  --principal-id $FUNCTION_PRINCIPAL_ID \
  --role-definition-name "Cosmos DB Built-in Data Contributor"
```

## Monitor Operations

### Application Insights Queries

Use these queries in Application Insights to monitor operation activity:

**Operations Started (Last 24 Hours):**
```kusto
traces
| where timestamp > ago(24h)
| where message contains "Creating operation"
| project timestamp, message
| extend operationId = extract("operation (.*?) for", 1, message)
| extend blobName = extract("blob (.*)", 1, message)
```

**Operations by Status:**
```kusto
customEvents
| where name == "OperationStatusChanged"
| summarize count() by tostring(customDimensions.Status)
```

**Failed Operations:**
```kusto
traces
| where timestamp > ago(24h)
| where message contains "Error" and message contains "operation"
| project timestamp, message, severityLevel
```

### Cosmos DB Metrics

Monitor Cosmos DB container metrics in Azure Portal:
1. Navigate to Cosmos DB account
2. Go to **Metrics**
3. Add metrics:
   - Total Request Units
   - Total Requests
   - Data + Index Storage

## Troubleshooting

### Operations Container Not Found

**Error:** `Cosmos DB container not found`

**Solution:**
- Verify the Operations container exists in Cosmos DB
- Check the container name in app settings matches "Operations"
- Verify database name is "DocumentOcrDb"

### 404 Not Found on API Calls

**Error:** HTTP 404 when calling `/api/operations`

**Solution:**
- Verify function deployment completed successfully
- Check function app logs for startup errors
- Verify the function host is running

### Unauthorized (401) Errors

**Error:** HTTP 401 Unauthorized

**Solution:**
- Verify function key is included in request: `?code=<key>`
- Check function authorization level in code
- Regenerate function keys if needed

### Operation Status Not Updating

**Issue:** Operation status stuck in "NotStarted"

**Solution:**
- Check if queue message was sent successfully
- Verify Azure Storage Queue exists and is accessible
- Check PdfProcessorFunction logs for errors
- Verify operation ID is included in queue message

## Integration Examples

See [OPERATIONS-API.md](OPERATIONS-API.md) for complete integration examples in:
- JavaScript/TypeScript
- C#
- Python

## Next Steps

1. **Configure CORS** if calling from web applications
2. **Set up API Management** for rate limiting and caching
3. **Configure monitoring alerts** for failed operations
4. **Implement retention policy** for old operations
5. **Add custom metrics** for operation duration tracking

## Security Checklist

- [ ] Function keys configured and secured
- [ ] Managed Identity enabled for Cosmos DB access
- [ ] HTTPS enforced for all API calls
- [ ] CORS configured if needed
- [ ] API keys not committed to source control
- [ ] Application Insights configured for monitoring
- [ ] Private endpoints configured for production

## Support

For issues or questions:
- Review [OPERATIONS-API.md](OPERATIONS-API.md) for API documentation
- Check [ARCHITECTURE.md](ARCHITECTURE.md) for system design
- Review Application Insights logs for errors
- Check Azure Function logs in Azure Portal
