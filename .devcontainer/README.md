# Dev Container

This workspace runs in a dev container with the .NET 10 SDK, Azure CLI, PowerShell,
and the `azd` CLI, plus two emulators that live for the lifetime of the container:

| Service | Image                                                                  | Endpoint (inside the dev container)                                   |
| ------- | ---------------------------------------------------------------------- | --------------------------------------------------------------------- |
| Azurite | `mcr.microsoft.com/azure-storage/azurite:latest`                       | `http://127.0.0.1:10000` (blob) / `:10001` (queue) / `:10002` (table) |
| Cosmos  | `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview` | `https://127.0.0.1:8081`                                              |

Both emulators share the dev container's network namespace via
`network_mode: service:dev` in [docker-compose.yml](docker-compose.yml), so the
WebApp and Functions reach them on `localhost` exactly like a developer's laptop.
`shutdownAction: stopCompose` and `restart: unless-stopped` keep them alive for
as long as the dev container is running.

## Provisioning

[`post-create.sh`](post-create.sh) runs automatically after the container is
created and is fully idempotent. It:

1. Waits for Azurite and the Cosmos emulator to become responsive.
2. Imports the Cosmos emulator's self-signed cert into the system trust store.
3. Creates the Azurite blob containers `uploaded-pdfs` and `processed-documents`.
4. Creates the Azurite queue `pdf-processing-queue`.
5. Creates the Cosmos database `DocumentOcrDb` with containers `ProcessedDocuments`
   (partition key `/identifier`) and `Operations` (partition key `/id`).

Re-run manually with `bash .devcontainer/post-create.sh` if you need to recreate
resources after wiping the Azurite volume or restarting the Cosmos emulator.

## Connecting from app code

The settings templates already contain the well-known emulator credentials:

- `src/DocumentOcr.Processor/local.settings.json.template` — Azurite connection
  string + Cosmos endpoint/key for the Functions host.
- `src/DocumentOcr.WebApp/appsettings.Development.json.template` — same values
  for the Blazor WebApp.

The runtime services prefer the connection string / key when present and fall
back to `DefaultAzureCredential` otherwise, so production deployments are
unaffected.
