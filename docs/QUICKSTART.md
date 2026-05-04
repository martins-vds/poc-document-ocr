# Quick Start (Local Development)

Run the Function App, the Web App, and the test suite on your laptop using the helper scripts in [`scripts/`](../scripts/).

> 🔐 **Keyless auth.** All clients use `DefaultAzureCredential` and your `az login` identity. There are **no API keys** in any settings file.

## Prerequisites

| Tool                                                                                                   | Why                                                           | Install                               |
| ------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------- | ------------------------------------- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)                                       | All projects target `net10.0`.                                | `dotnet --version` should print 10.x. |
| [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) | Hosts the `PdfProcessorFunction` and `OperationsApi` locally. | `func --version` should print 4.x.    |
| [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)                        | Local Azure Storage emulator (queues + blobs).                | `npm install -g azurite`              |
| [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)                                   | Auth + role assignment.                                       | `az login`                            |
| Active Azure subscription                                                                              | For Document Intelligence and Cosmos DB (no free emulator).   | —                                     |

## 1. Sign in to Azure

```bash
az login
az account set --subscription "<your-subscription>"
```

`DefaultAzureCredential` will pick up these credentials automatically.

## 2. Configure local settings

There is **no shared keys file** — each app has its own template.

```bash
# Function App
cp src/DocumentOcr.Processor/local.settings.json.template \
   src/DocumentOcr.Processor/local.settings.json

# Web App
cp src/DocumentOcr.WebApp/appsettings.Development.json.template \
   src/DocumentOcr.WebApp/appsettings.Development.json
```

Fill in:

- **`DocumentIntelligence:Endpoint`** — must end with `/`.
- **`CosmosDb:Endpoint`** — Cosmos account URI (`https://<acct>.documents.azure.com:443/`).
- **`Storage:AccountName`** — `devstoreaccount1` for Azurite, real account name otherwise.
- **`AzureAd:*`** (Web App only) — your Entra ID app registration.
- **`DocumentProcessing:IdentifierFieldName`** — DI field used to group pages into documents (default `identifier`). See [CUSTOMIZING-SCHEMA.md](CUSTOMIZING-SCHEMA.md).

> If you ran `azd provision` already, the postprovision hook auto-fills both files via [`utils/update_settings.py`](../utils/update_settings.py).

## 3. Grant your user the same RBAC the deployed app has

Your `az login` identity needs read/write on the same resources the deployed Managed Identity uses:

| Service                        | Role                                                                                          |
| ------------------------------ | --------------------------------------------------------------------------------------------- |
| Storage account (blob + queue) | `Storage Blob Data Contributor`, `Storage Queue Data Contributor`                             |
| Document Intelligence          | `Cognitive Services User`                                                                     |
| Cosmos DB account              | `Cosmos DB Built-in Data Contributor` (assigned via `az cosmosdb sql role assignment create`) |

The [role-assignment recipe](#appendix-role-assignment-recipe) at the bottom is a copy-pasteable version.

## 4. Start Azurite

```bash
azurite --silent --location /tmp/azurite &
```

Create the queue + containers it needs:

```bash
az storage queue     create --name pdf-processing-queue --connection-string "UseDevelopmentStorage=true"
az storage container create --name uploaded-pdfs        --connection-string "UseDevelopmentStorage=true"
az storage container create --name processed-documents  --connection-string "UseDevelopmentStorage=true"
```

## 5. Run things

```bash
# Function App (queue trigger + Operations API on http://localhost:7071)
./scripts/run-functions.sh        # bash
./scripts/run-functions.ps1       # PowerShell

# Blazor Web App (default https://localhost:7227)
./scripts/run-webapp.sh           # bash
./scripts/run-webapp.ps1          # PowerShell

# Unit tests
./scripts/run-tests.sh            # bash
./scripts/run-tests.ps1           # PowerShell
```

See [`scripts/README.md`](../scripts/README.md) for all flags (`--no-build`, `--filter`, `--coverage`, `--urls`, …).

## 6. Smoke-test the pipeline

1. Open the Web App, sign in with Entra ID, go to **Upload**, drop a sample PDF.
2. Watch progress on the **Operations** page (auto-refresh every 10 s).
3. When the operation succeeds, open **Documents → Review** and verify the schema fields render with confidence badges.

You can also enqueue messages directly to bypass the Web App; see [docs/OPERATIONS-API.md](OPERATIONS-API.md) for the request body and the queue-message wrapper format.

## Debugging

- **VS Code** — open the workspace, press <kbd>F5</kbd>. The `DocumentOcr.slnLaunch.user` profile starts both projects.
- **Visual Studio** — open `DocumentOcr.sln` and start both projects (set as multiple startup projects).

## Common issues

| Symptom                                                       | Fix                                                                                                                                    |
| ------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Function host won't start, "FUNCTIONS_WORKER_RUNTIME not set" | Confirm `local.settings.json` exists and contains `"FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"`.                                     |
| `403` from Document Intelligence                              | Endpoint must end with `/`. Your CLI identity needs `Cognitive Services User`.                                                         |
| `401`/`403` from Cosmos                                       | Run the `az cosmosdb sql role assignment create` command in the appendix.                                                              |
| Queue trigger never fires                                     | Azurite isn't running, or queue `pdf-processing-queue` doesn't exist.                                                                  |
| Web App shows "no documents"                                  | Are you signed in as a user the Function App processed under? Documents are filterable by reviewer assignment, not by tenant identity. |

## Appendix: role-assignment recipe

```bash
USER_ID=$(az ad signed-in-user show --query id -o tsv)
SUB=$(az account show --query id -o tsv)
RG=<your-resource-group>

# Storage
az role assignment create --assignee $USER_ID \
  --role "Storage Blob Data Contributor" \
  --scope "/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.Storage/storageAccounts/<storage-account>"
az role assignment create --assignee $USER_ID \
  --role "Storage Queue Data Contributor" \
  --scope "/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.Storage/storageAccounts/<storage-account>"

# Document Intelligence
az role assignment create --assignee $USER_ID \
  --role "Cognitive Services User" \
  --scope "/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.CognitiveServices/accounts/<doc-intel>"

# Cosmos DB (data plane RBAC, separate command)
az cosmosdb sql role assignment create \
  --account-name <cosmos-acct> --resource-group $RG \
  --role-definition-name "Cosmos DB Built-in Data Contributor" \
  --principal-id $USER_ID \
  --scope "/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.DocumentDB/databaseAccounts/<cosmos-acct>"
```

## Next steps

- [docs/CUSTOMIZING-SCHEMA.md](CUSTOMIZING-SCHEMA.md) — change which fields the system reviews.
- [docs/ARCHITECTURE.md](ARCHITECTURE.md) — full system design.
- [docs/DEPLOYMENT-IAC.md](DEPLOYMENT-IAC.md) — deploy with `azd up`.
- [docs/TESTING.md](TESTING.md) — test layout and how to add tests.
