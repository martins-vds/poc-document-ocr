# Quick Deploy Guide

Get the Document OCR Processor running in Azure in under 20 minutes.

## Prerequisites

Install these tools (one-time setup):

- [Azure Developer CLI (azd)](https://aka.ms/azd-install)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)

## Deploy (5 commands)

```bash
# 1. Login to Azure
azd auth login

# 2. Create environment
azd env new dev

# 3. Set location
azd env set AZURE_LOCATION eastus

# 4. Deploy everything (infrastructure + code)
azd up

# 5. Done! Get your function app name
azd env get-value AZURE_FUNCTION_NAME
```

**That's it!** Your application is deployed with:
- ✅ **Azure Verified Modules** - Microsoft-supported Bicep modules
- ✅ Private networking (no public access)
- ✅ Managed identity authentication
- ✅ Azure Storage, Document Intelligence, Cosmos DB
- ✅ Monitoring with Application Insights

> **Note**: By default, deployment uses [Azure Verified Modules (AVM)](https://aka.ms/avm) from the Bicep Public Registry for production-grade infrastructure.

## Test It

Upload a PDF and trigger processing:

```bash
# Get storage account name
STORAGE_ACCOUNT=$(azd env get-value AZURE_STORAGE_ACCOUNT_NAME)

# Upload a test PDF (replace with your PDF file)
az storage blob upload \
  --account-name $STORAGE_ACCOUNT \
  --container-name uploaded-pdfs \
  --name test.pdf \
  --file /path/to/your/document.pdf \
  --auth-mode login

# Send queue message to trigger processing
az storage message put \
  --account-name $STORAGE_ACCOUNT \
  --queue-name pdf-processing-queue \
  --content '{"BlobName":"test.pdf","ContainerName":"uploaded-pdfs"}' \
  --auth-mode login

# Check processed results (wait ~30 seconds)
az storage blob list \
  --account-name $STORAGE_ACCOUNT \
  --container-name processed-documents \
  --auth-mode login \
  --output table
```

## View Logs

```bash
# Stream function logs
FUNCTION_APP=$(azd env get-value AZURE_FUNCTION_NAME)
func azure functionapp logstream $FUNCTION_APP
```

## Update Code

After making changes to the function code:

```bash
azd deploy
```

## Cleanup

Remove all Azure resources:

```bash
azd down
```

## Next Steps

- [Full IaC Documentation](infra/README.md) - Technical details
- [Deployment Guide](docs/DEPLOYMENT-IAC.md) - Step-by-step instructions
- [Architecture](docs/ARCHITECTURE.md) - How it works
- [Testing Guide](docs/TESTING.md) - Testing information

## Need Help?

- Check [Troubleshooting](docs/DEPLOYMENT-IAC.md#troubleshooting) section
- Review [Azure Developer CLI Docs](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- See [Issue Tracker](https://github.com/martins-vds/poc-document-ocr/issues)
