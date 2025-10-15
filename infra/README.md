# Infrastructure as Code (Bicep)

This directory contains Infrastructure as Code (IaC) scripts for deploying the Document OCR Processor to Azure using Bicep and Azure Developer CLI (azd).

## Deployment Options

This repository provides **two Bicep deployment options**:

### 1. Azure Verified Modules (AVM) - **Recommended** 
- **File**: `main-avm.bicep`
- Uses official [Azure Verified Modules](https://aka.ms/avm) from Bicep Public Registry
- Microsoft-supported and regularly updated modules
- Best practices and security built-in
- Standardized parameter names and outputs
- **Use this for production deployments**

### 2. Custom Modules
- **File**: `main.bicep` 
- Uses custom Bicep modules in `modules/` folder
- Simpler, more readable for learning
- Good for understanding Bicep fundamentals
- Useful when network connectivity to Bicep registry is limited

Both options deploy the same infrastructure with identical security and networking configurations.

## Architecture

The infrastructure deploys the following Azure resources with **private networking** (no public access):

- **Virtual Network** with dedicated subnets for Function App integration and private endpoints
- **Private DNS Zones** for all Azure services
- **Storage Account** with private endpoints for blob, queue, and table storage
- **Document Intelligence** (Form Recognizer) with private endpoint
- **Cosmos DB** (serverless) with private endpoint
- **Azure Functions** (Linux, Premium P1v3) with VNet integration and private endpoint
- **Application Insights** and Log Analytics for monitoring
- **Role-based Access Control (RBAC)** at resource level using Managed Identity

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) (version 2.50.0 or later)
- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) (version 1.5.0 or later)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- Active Azure subscription with permissions to create resources and assign roles

## Quick Start with Azure Developer CLI

### 1. Initialize the environment

```bash
# Login to Azure
azd auth login

# Initialize a new environment (creates .azure folder)
azd env new <environment-name>

# Example: azd env new dev
```

### 2. Set environment variables

```bash
# Set the Azure location
azd env set AZURE_LOCATION eastus

# Optionally, set a specific subscription
azd env set AZURE_SUBSCRIPTION_ID <your-subscription-id>
```

### 3. Provision infrastructure

**Option A: Using Azure Verified Modules (Recommended)**

```bash
# Deploy using AVM modules from Bicep Public Registry
azd provision --template infra/main-avm.bicep
```

**Option B: Using Custom Modules**

```bash
# Deploy using custom modules (default)
azd provision
```

This command will:
- Create a resource group (if needed)
- Deploy all Bicep modules (from registry or local)
- Set up networking with private endpoints
- Configure RBAC permissions

The deployment takes approximately 10-15 minutes.

### 4. Deploy function code

```bash
# Deploy the function app code
azd deploy
```

Or use the utility script:

```bash
# Linux/macOS
./infra/scripts/deploy-function.sh <function-app-name>

# Windows PowerShell
./infra/scripts/deploy-function.ps1 -FunctionAppName <function-app-name>
```

To get the function app name after provisioning:

```bash
azd env get-values | grep FUNCTION_APP_NAME
```

### 5. Monitor deployment

```bash
# View all deployed resources
azd show

# View environment values
azd env get-values
```

## Manual Deployment with Azure CLI

If you prefer to deploy without azd:

### 1. Create a resource group

```bash
az group create \
  --name rg-documentocr-dev \
  --location eastus
```

### 2. Deploy the Bicep template

**Option A: Using Azure Verified Modules (Recommended)**

```bash
az deployment group create \
  --resource-group rg-documentocr-dev \
  --template-file infra/main-avm.bicep \
  --parameters environmentName=dev location=eastus
```

**Option B: Using Custom Modules**

```bash
az deployment group create \
  --resource-group rg-documentocr-dev \
  --template-file infra/main.bicep \
  --parameters environmentName=dev location=eastus
```

### 3. Get outputs

```bash
az deployment group show \
  --resource-group rg-documentocr-dev \
  --name main \
  --query properties.outputs
```

### 4. Deploy function code

Use the utility scripts in `infra/scripts/` or Azure Functions Core Tools:

```bash
cd src
func azure functionapp publish <function-app-name>
```

## Azure Verified Modules (AVM) vs Custom Modules

### Why Use Azure Verified Modules?

[Azure Verified Modules (AVM)](https://aka.ms/avm) are the official, Microsoft-supported way to deploy Azure resources using Bicep:

**Benefits:**
- ✅ **Microsoft Support** - Officially maintained by Microsoft
- ✅ **Best Practices** - Security, networking, and governance built-in
- ✅ **Regular Updates** - Keep up with Azure platform changes
- ✅ **Standardization** - Consistent parameter names across modules
- ✅ **Community Tested** - Used by thousands of deployments
- ✅ **Comprehensive** - Covers all Azure resource types
- ✅ **Versioned** - Semantic versioning for stable deployments

**When to Use Custom Modules:**
- 🔧 Learning Bicep fundamentals
- 🔧 Air-gapped or restricted network environments
- 🔧 Specific customizations not supported by AVM
- 🔧 Simpler, more readable code for POC projects

### Module Comparison

| Feature | AVM (main-avm.bicep) | Custom (main.bicep) |
|---------|---------------------|---------------------|
| Source | Bicep Public Registry | Local `modules/` folder |
| Maintenance | Microsoft | User |
| Updates | Automatic (versioned) | Manual |
| Validation | Extensive testing | User validation |
| Recommended for | Production | Learning/POC |

### Module References

The AVM template uses the following verified modules:

- `avm/res/network/virtual-network` - Virtual Networks and Subnets
- `avm/res/network/private-dns-zone` - Private DNS Zones
- `avm/res/operational-insights/workspace` - Log Analytics
- `avm/res/insights/component` - Application Insights
- `avm/res/storage/storage-account` - Storage Accounts with Private Endpoints
- `avm/res/cognitive-services/account` - Cognitive Services (Document Intelligence)
- `avm/res/document-db/database-account` - Cosmos DB
- `avm/res/web/serverfarm` - App Service Plans
- `avm/res/web/site` - Function Apps

See the [AVM Module Index](https://azure.github.io/Azure-Verified-Modules/) for complete documentation.

## Utility Scripts

### Build Function Locally

Build the function app without deploying:

```bash
# Linux/macOS
./infra/scripts/build-function.sh

# Windows PowerShell
./infra/scripts/build-function.ps1
```

This script:
- Cleans previous builds
- Restores NuGet packages
- Builds the project in Release configuration
- Publishes to `src/publish` folder

### Deploy Function to Azure

Deploy the built function app to Azure:

```bash
# Linux/macOS
./infra/scripts/deploy-function.sh <function-app-name>

# Windows PowerShell
./infra/scripts/deploy-function.ps1 -FunctionAppName <function-app-name>
```

This script:
- Validates prerequisites (Azure Functions Core Tools, Azure CLI login)
- Builds the function app
- Deploys to the specified Function App

## Infrastructure Details

### Network Isolation

All resources are deployed with **no public network access**:

- Storage Account: `publicNetworkAccess: Disabled`
- Document Intelligence: `publicNetworkAccess: Disabled`
- Cosmos DB: `publicNetworkAccess: Disabled`
- Function App: `publicNetworkAccess: Disabled` with VNet integration

Private endpoints are configured for all services, connected to a dedicated subnet with proper DNS resolution.

### Role Assignments (Least Privilege)

The Function App uses a **system-assigned managed identity** with the following resource-level permissions:

**Storage Account:**
- Storage Blob Data Contributor
- Storage Queue Data Contributor
- Storage Table Data Contributor

**Document Intelligence:**
- Cognitive Services User

**Cosmos DB:**
- Cosmos DB Built-in Data Contributor

No API keys are stored in configuration. The Function App authenticates using its managed identity.

### Function App Configuration

The Function App is configured with:
- **Runtime:** .NET 8 Isolated
- **OS:** Linux
- **Tier:** Premium P1v3 (required for VNet integration)
- **VNet Integration:** Enabled for outbound traffic
- **Always On:** Enabled for better performance

### Bicep Modules

The infrastructure is organized into modular Bicep files:

- `main.bicep` - Main orchestration template
- `modules/vnet.bicep` - Virtual network and subnets
- `modules/privateDnsZones.bicep` - Private DNS zones for all services
- `modules/logAnalytics.bicep` - Log Analytics workspace
- `modules/applicationInsights.bicep` - Application Insights
- `modules/storage.bicep` - Storage account with private endpoints
- `modules/documentIntelligence.bicep` - Document Intelligence with private endpoint
- `modules/cosmosDb.bicep` - Cosmos DB with private endpoint
- `modules/appServicePlan.bicep` - App Service Plan (Linux)
- `modules/functionApp.bicep` - Function App with VNet integration
- `modules/roleAssignments.bicep` - RBAC role assignments

## Parameters

The main template accepts the following parameters:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `location` | string | Resource group location | Azure region for resources |
| `environmentName` | string | `dev` | Environment name (dev/test/prod) |
| `workloadName` | string | `documentocr` | Workload identifier |
| `tags` | object | Auto-generated | Tags to apply to resources |

## Outputs

After deployment, the following values are available:

- `storageAccountName` - Storage account name
- `documentIntelligenceName` - Document Intelligence resource name
- `documentIntelligenceEndpoint` - Document Intelligence endpoint
- `cosmosDbAccountName` - Cosmos DB account name
- `cosmosDbEndpoint` - Cosmos DB endpoint
- `functionAppName` - Function App name
- `functionAppUrl` - Function App URL
- `applicationInsightsName` - Application Insights name
- `resourceGroupName` - Resource group name

## Cleanup

To delete all deployed resources:

### Using azd

```bash
azd down
```

### Using Azure CLI

```bash
az group delete --name rg-documentocr-dev --yes --no-wait
```

## Troubleshooting

### Function App not starting

- Verify VNet integration is properly configured
- Check that private endpoints are resolving correctly
- Review Application Insights logs for errors

### Cannot access storage from Function App

- Ensure role assignments completed successfully
- Verify private DNS zones are linked to the VNet
- Check that managed identity is enabled on the Function App

### Deployment timeout

- Network-isolated deployments take longer (10-15 minutes)
- Private endpoint provisioning can take 5-10 minutes
- Role assignment propagation may take up to 5 minutes

### Function deployment fails

- Ensure Azure Functions Core Tools v4 is installed
- Verify you're logged into Azure CLI (`az login`)
- Check that the function app name is correct
- Try building locally first with `build-function.sh/ps1`

## Cost Optimization

The default configuration uses:
- Premium P1v3 App Service Plan (required for private networking)
- Cosmos DB Serverless (pay-per-request)
- Standard LRS storage
- Standard Document Intelligence S0

For development/testing, consider:
- Stopping the Function App when not in use
- Using smaller App Service Plan tier if private networking isn't required
- Adjusting Cosmos DB throughput based on usage

## Security Considerations

- All resources use private networking
- No API keys in configuration (managed identity authentication)
- HTTPS-only communication enforced
- Minimum TLS version 1.2
- Storage account disables public blob access
- Network ACLs deny public traffic by default

## Additional Resources

- [Azure Bicep Documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [Azure Developer CLI Documentation](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- [Azure Functions on Linux](https://docs.microsoft.com/azure/azure-functions/functions-create-first-azure-function-azure-cli?tabs=linux)
- [Private Endpoints](https://docs.microsoft.com/azure/private-link/private-endpoint-overview)
- [VNet Integration](https://docs.microsoft.com/azure/azure-functions/functions-networking-options)
