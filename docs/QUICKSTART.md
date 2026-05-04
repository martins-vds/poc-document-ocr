# Quick Start (Local Development)

Run the Function App, the Web App, and the test suite on your laptop using the helper scripts in [`scripts/`](../scripts/).

> 🔐 **Keyless auth (production path).** Deployed clients use `DefaultAzureCredential` against managed identities. For local development against the emulators, the settings templates ship with the well-known Azurite connection string and Cosmos DB emulator key — see [Option A](#option-a-recommended--vs-code-dev-container) below.

## Two ways to run locally

- **[Option A — VS Code dev container](#option-a-recommended--vs-code-dev-container)** (recommended). Spins up the .NET 10 SDK, Azurite, and the Cosmos DB emulator side-by-side and auto-provisions queues, blob containers, and Cosmos containers. No Azure subscription is required for storage or Cosmos.
- **[Option B — Bare metal](#option-b-bare-metal)**. Install the SDKs locally and run Azurite manually. Use this if you cannot use Docker.

Document Intelligence has no emulator either way — you still need an Azure endpoint for OCR, but the rest of the pipeline runs offline.

## Option A (recommended) — VS Code dev container

The [`.devcontainer/`](../.devcontainer/) folder defines a Compose-based environment with three services that stay up for the lifetime of the container:

| Service   | Image                                                                  | Endpoint inside the container                                       |
| --------- | ---------------------------------------------------------------------- | ------------------------------------------------------------------- |
| `dev`     | `mcr.microsoft.com/devcontainers/dotnet:2-10.0-noble`                  | (your shell)                                                        |
| `azurite` | `mcr.microsoft.com/azure-storage/azurite:latest`                       | `http://127.0.0.1:10000` (blob), `:10001` (queue), `:10002` (table) |
| `cosmos`  | `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview` | `https://127.0.0.1:8081`                                            |

Both emulators share the dev container's network namespace via `network_mode: service:dev`, so the WebApp and Functions reach them on `localhost` exactly like a developer laptop.

### Steps

1. **Open the workspace in the dev container.** VS Code will prompt to reopen in container, or run **Dev Containers: Reopen in Container** from the command palette.
2. **Wait for `postCreateCommand` to finish.** It runs [`.devcontainer/post-create.sh`](../.devcontainer/post-create.sh), which (idempotently):
   - waits for both emulators to be responsive,
   - imports the Cosmos emulator's self-signed certificate into the system trust store,
   - creates the Azurite blob containers `uploaded-pdfs` and `processed-documents`,
   - creates the Azurite queue `pdf-processing-queue`,
   - creates the Cosmos database `DocumentOcrDb` with containers `ProcessedDocuments` (PK `/identifier`) and `Operations` (PK `/id`) by calling [`provision-cosmos.py`](../.devcontainer/provision-cosmos.py).
3. **Copy the settings templates** (their defaults already point at the emulators):
   ```bash
   cp src/DocumentOcr.Processor/local.settings.json.template \
      src/DocumentOcr.Processor/local.settings.json
   cp src/DocumentOcr.WebApp/appsettings.Development.json.template \
      src/DocumentOcr.WebApp/appsettings.Development.json
   ```
4. **Fill in the Azure-only values** that have no emulator:
   - `DocumentIntelligence:Endpoint` (must end with `/`),
   - `AzureAd:*` (Web App only) — your Entra ID app registration.
5. **Skip to [§5 Run things](#5-run-things).** RBAC (§3) and the Azurite bootstrap (§4) are unnecessary.

Re-run the provisioning script any time with `bash .devcontainer/post-create.sh` — it is fully idempotent.

## Option B — bare metal

### Prerequisites

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

> Skip this section if you are on the dev container \u2014 Azurite is already running and the queue + containers were created automatically.\n\n```bash\nazurite --silent --location /tmp/azurite &\n```

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

| Symptom                                                       | Fix                                                                                                                                                                                                                                                               |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Function host won't start, "FUNCTIONS_WORKER_RUNTIME not set" | Confirm `local.settings.json` exists and contains `"FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"`.                                                                                                                                                                |
| `403` from Document Intelligence                              | Endpoint must end with `/`. Your CLI identity needs `Cognitive Services User`.                                                                                                                                                                                    |
| `401`/`403` from Cosmos                                       | Run the `az cosmosdb sql role assignment create` command in the appendix.                                                                                                                                                                                         |
| Queue trigger never fires                                     | Azurite isn't running, or queue `pdf-processing-queue` doesn't exist.                                                                                                                                                                                             |
| Cosmos emulator returns SSL/TLS error                         | The dev-container `post-create.sh` imports the emulator cert into the system trust store. Outside the dev container, set `CosmosDb:Key` in the WebApp / Functions settings to the well-known emulator key \u2014 the client will then trust the self-signed cert. |
| Web App shows "no documents"                                  | Are you signed in as a user the Function App processed under? Documents are filterable by reviewer assignment, not by tenant identity.                                                                                                                            |

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

- [.devcontainer/README.md](../.devcontainer/README.md) — dev-container layout, emulator endpoints, and provisioning details.
- [docs/CUSTOMIZING-SCHEMA.md](CUSTOMIZING-SCHEMA.md) — change which fields the system reviews.
- [docs/ARCHITECTURE.md](ARCHITECTURE.md) — full system design.
- [docs/DEPLOYMENT-IAC.md](DEPLOYMENT-IAC.md) — deploy with `azd up`.
- [docs/TESTING.md](TESTING.md) — test layout and how to add tests.
