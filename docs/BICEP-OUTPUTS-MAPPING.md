# Bicep Outputs to Environment Variables Mapping

This document shows how the postprovision scripts retrieve Bicep outputs and set environment variables for keyless authentication that the configuration utilities expect.

## Mapping Table

| Bicep Output | Retrieved via | Environment Variable Set | Used By | Description |
|--------------|---------------|-------------------------|---------|-------------|
| `AZURE_STORAGE_ACCOUNT_NAME` | `azd env get-value AZURE_STORAGE_ACCOUNT_NAME` | `AZURE_STORAGE_ACCOUNT_NAME` | update_settings.py | Storage account name for keyless authentication |
| `AZURE_DOCUMENTINTELLIGENCE_NAME` | `azd env get-value AZURE_DOCUMENTINTELLIGENCE_NAME` | - | postprovision scripts | Document Intelligence service name (informational) |
| `AZURE_DOCUMENTINTELLIGENCE_ENDPOINT` | `azd env get-value AZURE_DOCUMENTINTELLIGENCE_ENDPOINT` | `AZURE_DOCUMENTINTELLIGENCE_ENDPOINT` | update_settings.py | Document Intelligence service endpoint URL |
| `AZURE_COSMOSDB_ACCOUNT_NAME` | `azd env get-value AZURE_COSMOSDB_ACCOUNT_NAME` | - | postprovision scripts | Cosmos DB account name (informational) |
| `AZURE_COSMOSDB_ENDPOINT` | `azd env get-value AZURE_COSMOSDB_ENDPOINT` | `AZURE_COSMOSDB_ENDPOINT` | update_settings.py | Cosmos DB endpoint URL |
| `AZURE_COSMOSDB_DATABASE` | Direct value | `AZURE_COSMOSDB_DATABASE` | update_settings.py | Cosmos DB database name (DocumentOcrDb) |
| `AZURE_COSMOSDB_CONTAINER` | Direct value | `AZURE_COSMOSDB_CONTAINER` | update_settings.py | Cosmos DB container name (ProcessedDocuments) |
| `AZURE_TENANT_ID` | Bicep parameter | `AZURE_TENANT_ID` | update_settings.py | Azure AD tenant ID for web app authentication |
| `AZURE_WEB_APP_CLIENT_ID` | Bicep parameter | `AZURE_WEB_APP_CLIENT_ID` | update_settings.py | Azure AD client ID for web app authentication |
| `AZURE_AD_DOMAIN` | Bicep parameter | `AZURE_AD_DOMAIN` | update_settings.py | Azure AD domain for web app authentication |
| `AZURE_FUNCTION_APP_URL` | `azd env get-value AZURE_FUNCTION_APP_URL` | `AZURE_OPERATIONS_API_URL` | update_settings.py | Operations API base URL (Function App URL) for web app to call |
| - | Direct value | `AZURE_OPERATIONS_API_KEY` | update_settings.py | Operations API function key (optional, empty for local dev) |

## How It Works

1. **Bicep outputs** are made available through `azd env get-value <outputName>` using exact output names

2. **Postprovision hooks** (`postprovision.sh` and `postprovision.ps1`) retrieve these values and set environment variables for keyless authentication (no keys or connection strings needed)

3. **Environment variables** are set by postprovision hooks for consumption by `utils/update_settings.py`

4. **Update settings utility** (`utils/update_settings.py`) uses these environment variables to configure local development files with keyless authentication settings (`local.settings.json` and `appsettings.Development.json`)

## Updated Bicep Outputs

The following outputs have been added or organized in `main-avm.bicep`:

```bicep
// Storage outputs
output AZURE_STORAGE_ACCOUNT_NAME string = storageAccountName

// Document Intelligence outputs  
output AZURE_DOCUMENTINTELLIGENCE_NAME string = documentIntelligenceName
output AZURE_DOCUMENTINTELLIGENCE_ENDPOINT string = documentIntelligence.outputs.endpoint

// Cosmos DB outputs
output AZURE_COSMOSDB_ACCOUNT_NAME string = cosmosDbAccountName
output AZURE_COSMOSDB_ENDPOINT string = cosmosDb.outputs.endpoint
output AZURE_COSMOSDB_DATABASE string = 'DocumentOcrDb'
output AZURE_COSMOSDB_CONTAINER string = 'ProcessedDocuments'

// Azure AD outputs (for Web App authentication)
output AZURE_TENANT_ID string = tenantId
output AZURE_WEB_APP_CLIENT_ID string = webAppClientId  
output AZURE_AD_DOMAIN string = azureAdDomain

// Function App outputs
output AZURE_FUNCTION_APP_NAME string = functionAppName
output AZURE_FUNCTION_APP_URL string = functionApp.outputs.defaultHostname

// Web App outputs
output AZURE_WEB_APP_NAME string = webAppName
output AZURE_WEB_APP_URL string = 'https://${webApp.outputs.defaultHostname}'

// Monitoring outputs
output AZURE_APPLICATION_INSIGHTS_NAME string = applicationInsightsName

// Infrastructure outputs
output AZURE_RESOURCE_GROUP_NAME string = resourceGroup().name
output AZURE_VNET_NAME string = vnet.outputs.name
output AZURE_VNET_ID string = vnet.outputs.resourceId
```

## Validation

All scripts now have access to the required environment variables through the azd environment, ensuring seamless integration between infrastructure provisioning and local development configuration.
