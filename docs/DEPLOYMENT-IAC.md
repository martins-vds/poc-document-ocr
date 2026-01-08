# Deployment Guide - Infrastructure as Code

This guide walks through deploying the Document OCR Processor using Infrastructure as Code (IaC) with Bicep and Azure Developer CLI (azd).

## Overview

The IaC deployment provides:
- **Automated infrastructure provisioning** using Bicep templates
- **Private networking** with no public access to resources
- **Managed identity authentication** (no API keys in configuration)
- **Role-based access control** at the resource level
- **Consistent environments** across dev/test/prod

## Prerequisites

1. **Azure Developer CLI (azd)**
   ```bash
   # Windows (PowerShell)
   winget install microsoft.azd
   
   # macOS
   brew tap azure/azd
   brew install azd
   
   # Linux
   curl -fsSL https://aka.ms/install-azd.sh | bash
   ```

2. **Azure CLI**
   ```bash
   # Follow instructions at: https://learn.microsoft.com/cli/azure/install-azure-cli
   ```

3. **.NET 8.0 SDK**
   ```bash
   # Download from: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
   ```

4. **Azure Functions Core Tools v4**
   ```bash
   # Windows (PowerShell)
   winget install Microsoft.Azure.FunctionsCoreTools
   
   # macOS
   brew tap azure/functions
   brew install azure-functions-core-tools@4
   
   # Linux (may require sudo)
   sudo npm install -g azure-functions-core-tools@4
   # Or use distribution-specific package manager
   ```

5. **Active Azure Subscription**
   - With permissions to create resources
   - With permissions to assign roles

## Quick Start

### 1. Login to Azure

```bash
azd auth login
```

This will open a browser window for authentication.

### 2. Initialize Environment

```bash
# Create a new environment
azd env new <environment-name>

# Example:
azd env new dev
```

This creates a `.azure` folder with environment configuration.

### 3. Configure Location and Azure AD

```bash
# Set the Azure region
azd env set AZURE_LOCATION eastus

# Optionally, set a specific subscription
azd env set AZURE_SUBSCRIPTION_ID <your-subscription-id>

# Configure Azure AD for web app authentication
azd env set AZURE_TENANT_ID <your-tenant-id>
azd env set WEB_APP_CLIENT_ID <your-web-app-client-id>
azd env set AZURE_AD_DOMAIN <your-domain>  # e.g., contoso.onmicrosoft.com

# Note: You need to create an Azure AD app registration first
# See "Azure AD Configuration" section below for details
```

#### Azure AD Configuration

**IMPORTANT**: Complete this step BEFORE running the azd commands, as you'll need the CLIENT_ID for configuration.

Create an Azure AD app registration for the web application:

1. **Create App Registration**:
   - Navigate to Azure Portal → Azure Active Directory → App registrations
   - Click "New registration"
   - Name: `DocumentOcr.WebApp-dev` (or your environment name)
   - Redirect URI: You can use a placeholder initially like `https://localhost/signin-oidc`
   - Click "Register"

2. **Note the Application (client) ID** - you'll use this for `WEB_APP_CLIENT_ID` in the next step

3. **Note your Tenant ID and Domain**:
   - Tenant ID is shown on the Overview page
   - Domain is typically `yourcompany.onmicrosoft.com`

4. **Configure Authentication** (after deployment):
   - Under "Authentication", update the redirect URI to match your deployed app
   - Example: `https://app-documentocr-dev-abc123xyz.azurewebsites.net/signin-oidc`
   - The actual URL will be output after deployment
   - Enable "ID tokens" under Implicit grant and hybrid flows
   - Save changes

5. **API Permissions** (if needed):
   - Microsoft Graph → Delegated permissions → User.Read
   - Grant admin consent if required

### 4. Deploy Everything

```bash
# Deploy infrastructure and function code
azd up
```

This single command:
- Creates a resource group
- Deploys all Azure resources (Function App, Web App, Storage, Cosmos DB, etc.)
- Configures networking and private endpoints
- Sets up role assignments
- Deploys the function app and web app code

**Deployment time:** ~15-20 minutes (first deployment)

### 5. View Deployed Resources

```bash
# Show all resources
azd show

# Get environment values
azd env get-values
```

## Step-by-Step Deployment

If you prefer more control, you can deploy infrastructure and code separately:

### 1. Provision Infrastructure Only

```bash
azd provision
```

This deploys all Azure resources without deploying code.

### 2. Deploy Function Code

After infrastructure is provisioned:

```bash
azd deploy
```

Or use the utility scripts:

```bash
# Get the function app name
FUNCTION_APP_NAME=$(azd env get-value AZURE_FUNCTION_NAME)

# Linux/macOS
./infra/scripts/deploy-function.sh $FUNCTION_APP_NAME

# Windows PowerShell
$functionAppName = azd env get-value AZURE_FUNCTION_NAME
./infra/scripts/deploy-function.ps1 -FunctionAppName $functionAppName
```

## Infrastructure Details

### Resources Deployed

The deployment creates the following Azure resources:

| Resource | Purpose | SKU/Tier |
|----------|---------|----------|
| Virtual Network | Network isolation | 10.0.0.0/16 |
| Storage Account | Blob, Queue, Table storage | Standard LRS |
| Document Intelligence | OCR processing | S0 |
| Cosmos DB | Document storage | Serverless |
| Function App | Application hosting | Premium P1v3 |
| App Service Plan | Function compute | Premium P1v3 (Linux) |
| Application Insights | Monitoring | - |
| Log Analytics | Centralized logging | PerGB2018 |
| Private Endpoints | Private connectivity | 6 endpoints |
| Private DNS Zones | DNS resolution | 6 zones |

### Network Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   Virtual Network                       │
│                    (10.0.0.0/16)                       │
│  ┌─────────────────────┬──────────────────────────┐   │
│  │ Function Integration│  Private Endpoint Subnet │   │
│  │   Subnet            │      (10.0.2.0/24)       │   │
│  │  (10.0.1.0/24)     │                          │   │
│  │                     │  ┌────────────────────┐  │   │
│  │  ┌──────────────┐  │  │ Storage PE         │  │   │
│  │  │ Function App │◄─┼──┤ Doc Intelligence PE │  │   │
│  │  └──────────────┘  │  │ Cosmos DB PE       │  │   │
│  │                     │  │ Function App PE    │  │   │
│  └─────────────────────┴──┴────────────────────┴──┘   │
└─────────────────────────────────────────────────────────┘
```

### Security Features

- **No Public Access:** All resources deny public network traffic
- **Private Endpoints:** All communication through private endpoints
- **VNet Integration:** Function App integrated with VNet for outbound traffic
- **Managed Identity:** System-assigned identity for authentication
- **RBAC:** Least-privilege role assignments at resource level
- **TLS 1.2:** Minimum TLS version enforced
- **HTTPS Only:** No HTTP traffic allowed

### Role Assignments

The Function App is granted the following roles:

**Storage Account:**
- `Storage Blob Data Contributor` - Read/write blob containers
- `Storage Queue Data Contributor` - Read/write queues
- `Storage Table Data Contributor` - Read/write tables (Functions state)

**Document Intelligence:**
- `Cognitive Services User` - Use Document Intelligence API

**Cosmos DB:**
- `Cosmos DB Built-in Data Contributor` - Read/write to database

## Environment Variables

After deployment, the following environment variables are automatically configured:

| Variable | Description |
|----------|-------------|
| `FUNCTIONS_WORKER_RUNTIME` | Set to `dotnet-isolated` |
| `FUNCTIONS_EXTENSION_VERSION` | Set to `~4` |
| `AzureWebJobsStorage__accountName` | Storage account name (managed identity) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights connection |
| `DocumentIntelligence__Endpoint` | Document Intelligence endpoint |
| `CosmosDb__Endpoint` | Cosmos DB endpoint |
| `CosmosDb__DatabaseName` | Set to `DocumentOcrDb` |
| `CosmosDb__ContainerName` | Set to `ProcessedDocuments` |

## Customization

### Change Environment Name

The environment name is used as a suffix for resource names:

```bash
azd env new production
azd env set AZURE_LOCATION eastus
azd up
```

### Change Location

Deploy to a different Azure region:

```bash
azd env set AZURE_LOCATION westus2
azd provision
```

### Modify Resource Configuration

Edit the Bicep files in the `infra/` folder:

- `infra/main-avm.bicep` - Main template using Azure Verified Modules (recommended)
- `infra/modules/*.bicep` - Custom resource modules for complex scenarios

After changes, run:

```bash
azd provision
```

## Monitoring

### View Logs

```bash
# Stream function logs
func azure functionapp logstream <function-app-name>

# Or with azd
FUNCTION_APP_NAME=$(azd env get-value AZURE_FUNCTION_NAME)
func azure functionapp logstream $FUNCTION_APP_NAME
```

### Application Insights

After deployment, Application Insights is configured for monitoring:

1. Navigate to Azure Portal
2. Find Application Insights resource
3. View:
   - Live Metrics
   - Transaction search
   - Failures
   - Performance

### Log Analytics

Query logs using Kusto Query Language (KQL):

1. Navigate to Log Analytics workspace
2. Run queries like:

```kusto
FunctionAppLogs
| where TimeGenerated > ago(1h)
| order by TimeGenerated desc
```

## Updating the Deployment

### Update Infrastructure

```bash
# Pull latest changes
git pull

# Update infrastructure
azd provision
```

### Update Function Code

```bash
# Build and deploy
cd src
dotnet build --configuration Release
func azure functionapp publish <function-app-name>

# Or use azd
azd deploy

# Or use utility script
./infra/scripts/deploy-function.sh <function-app-name>
```

## Cleanup

### Delete All Resources

```bash
# Using azd (recommended)
azd down

# Or using Azure CLI
az group delete --name <resource-group-name> --yes --no-wait
```

To find your resource group name:

```bash
azd env get-value AZURE_RESOURCE_GROUP
```

## Troubleshooting

### Issue: Deployment Timeout

**Cause:** Private endpoint provisioning takes time

**Solution:** Wait for the deployment to complete. Private endpoint creation can take 5-10 minutes.

### Issue: Function App Not Starting

**Symptoms:** Function app shows as "Stopped" or errors in logs

**Solution:**
1. Verify VNet integration: `az functionapp vnet-integration list`
2. Check role assignments completed: View in Azure Portal → Function App → Identity
3. Review Application Insights for errors

### Issue: Cannot Access Storage

**Symptoms:** "403 Forbidden" errors when accessing storage

**Solution:**
1. Verify managed identity is enabled
2. Check role assignments: Storage Account → Access Control (IAM)
3. Wait up to 5 minutes for role propagation
4. Verify private DNS zone is linked to VNet

### Issue: Role Assignment Failures

**Symptoms:** Deployment fails at role assignment step

**Solution:**
1. Ensure you have `Owner` or `User Access Administrator` role
2. Check subscription has Microsoft.Authorization provider registered:
   ```bash
   az provider show --namespace Microsoft.Authorization
   # If not registered:
   az provider register --namespace Microsoft.Authorization
   ```
3. Review Azure Activity Log for detailed error

### Issue: Build Script Fails

**Symptoms:** Build script returns errors

**Solution:**
1. Ensure .NET 8.0 SDK is installed: `dotnet --version`
2. Run from project root: `./infra/scripts/build-function.sh`
3. Check for build errors: `cd src/DocumentOcr.Processor && dotnet build`

### Issue: Deploy Script Fails

**Symptoms:** Deploy script cannot find function app

**Solution:**
1. Verify function app name: `azd env get-value AZURE_FUNCTION_NAME`
2. Ensure logged into Azure: `az account show`
3. Check Azure Functions Core Tools installed: `func --version`

## Cost Considerations

### Estimated Monthly Costs (USD)

Based on moderate usage:

| Resource | Estimated Cost |
|----------|----------------|
| App Service Plan (P1v3) | ~$200 |
| Storage Account | ~$5-20 |
| Document Intelligence (S0) | Pay-per-use |
| Cosmos DB (Serverless) | Pay-per-request |
| Application Insights | ~$0-10 |
| Networking (Private Endpoints) | ~$7 per endpoint |

**Total:** ~$250-300/month + usage-based costs

### Cost Optimization Tips

1. **Stop when not in use:**
   ```bash
   az functionapp stop --name <function-app-name> --resource-group <rg-name>
   ```

2. **Use development environments sparingly**

3. **Scale down for testing:**
   - Edit `infra/modules/appServicePlan.bicep`
   - Change SKU from P1v3 to B1 (Basic)
   - Note: Basic tier doesn't support VNet integration

4. **Monitor usage:**
   - Set up cost alerts in Azure Portal
   - Review Azure Cost Management regularly

## Best Practices

1. **Use separate environments:** dev, test, prod
2. **Version control:** Track infrastructure changes in Git
3. **Secrets management:** Use Azure Key Vault (add to Bicep if needed)
4. **Monitoring:** Set up alerts in Application Insights
5. **Backup:** Enable geo-redundancy for production storage
6. **Testing:** Test infrastructure changes in dev first
7. **Documentation:** Document any customizations

## Additional Resources

- [Azure Developer CLI Documentation](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- [Bicep Documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [Azure Functions Networking](https://learn.microsoft.com/azure/azure-functions/functions-networking-options)
- [Private Endpoints](https://learn.microsoft.com/azure/private-link/private-endpoint-overview)
- [Managed Identity](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/overview)
- [RBAC in Azure](https://learn.microsoft.com/azure/role-based-access-control/overview)

## Next Steps

After deployment:

1. **Test the function:**
   - Upload a PDF to `uploaded-pdfs` container
   - Send a queue message with blob reference
   - Check `processed-documents` container for results

2. **Configure Logic App (optional):**
   - See [Logic App setup](DEPLOYMENT.md#step-4-set-up-logic-app-optional)
   - Use for email-based PDF submission

3. **Set up monitoring:**
   - Configure Application Insights alerts
   - Create dashboards for metrics

4. **Review security:**
   - Audit role assignments
   - Review network security groups
   - Enable Azure Defender (optional)
