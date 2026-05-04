# Developer scripts

Convenience scripts for working with the Document OCR POC locally.
Each script has a `bash` (`.sh`) and a PowerShell (`.ps1`) sibling so it
can run on Linux, macOS, and Windows.

| Script                                                           | What it does                                                               |
| ---------------------------------------------------------------- | -------------------------------------------------------------------------- |
| [run-functions.sh](run-functions.sh) / [.ps1](run-functions.ps1) | Build and start the Function App locally with `func start`.                |
| [run-webapp.sh](run-webapp.sh) / [.ps1](run-webapp.ps1)          | Build and run the Blazor Web App with `dotnet run`.                        |
| [run-tests.sh](run-tests.sh) / [.ps1](run-tests.ps1)             | Build and run the unit-test suite (`dotnet test`) with optional filtering. |

All scripts:

- Resolve paths relative to the repo root, so they work from any CWD.
- Require the **.NET 10 SDK** (matches the `TargetFramework` in every `*.csproj`).
- `run-functions.*` additionally requires **Azure Functions Core Tools v4** (`func`).
- Skip building when invoked with `--no-build` (or `-NoBuild` on PowerShell).

## Examples

```bash
# Start the function host on the default port
./scripts/run-functions.sh

# Start the web app on a custom port
./scripts/run-webapp.sh --urls http://localhost:5000

# Run only the schema mapper tests
./scripts/run-tests.sh --filter FullyQualifiedName~DocumentSchemaMapperServiceTests
```

```powershell
# Same on PowerShell (Windows / pwsh on macOS-Linux)
./scripts/run-functions.ps1
./scripts/run-webapp.ps1 -Urls http://localhost:5000
./scripts/run-tests.ps1 -Filter "FullyQualifiedName~DocumentSchemaMapperServiceTests"
```

## Pre-flight checklist

Before `run-functions.sh`/`.ps1`:

1. Copy `src/DocumentOcr.Processor/local.settings.json.template` to
   `local.settings.json` and fill in your endpoints.
2. Make sure Azurite is running (`azurite --silent --location /tmp/azurite &`).
3. Make sure you are signed in: `az login` — `DefaultAzureCredential` will
   use your CLI identity.

Before `run-webapp.sh`/`.ps1`:

1. Copy `src/DocumentOcr.WebApp/appsettings.Development.json.template` to
   `appsettings.Development.json` and fill in your endpoints + Entra ID
   client / tenant IDs.

`run-tests.*` has no runtime dependencies beyond the .NET SDK.
