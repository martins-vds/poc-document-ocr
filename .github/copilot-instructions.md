# Copilot Agent Instructions for Document OCR Processor

## Repository Overview

**Azure Functions v4 application** (.NET 8.0, dotnet-isolated runtime) that processes multi-document PDFs using Azure Document Intelligence for data extraction and Cosmos DB for persistence. 16 files, 9 unit tests, POC status.

**Key Dependencies**: PdfSharpCore, Azure.AI.FormRecognizer, Azure.Storage.Blobs, Microsoft.Azure.Cosmos

## Project Structure

```
src/                              # ALWAYS run build commands from here
├── Functions/PdfProcessorFunction.cs      # Entry point: queue-triggered function
├── Services/
│   ├── PdfToImageService.cs               # PDF to image conversion
│   ├── ImageToPdfService.cs               # Image to PDF conversion
│   ├── DocumentIntelligenceService.cs     # OCR extraction (86 lines)
│   ├── DocumentAggregatorService.cs       # Page aggregation by identifier
│   ├── BlobStorageService.cs              # Blob storage operations
│   └── CosmosDbService.cs                 # Cosmos DB persistence
├── Models/                                # QueueMessage, DocumentResult, ProcessingResult, etc.
├── Program.cs                             # DI setup - register new services here
├── DocumentOcrProcessor.csproj            # Project file with NuGet packages
├── host.json                              # Functions runtime config
└── local.settings.json.template           # Copy to local.settings.json for dev

docs/           # ARCHITECTURE.md, DEPLOYMENT.md, QUICKSTART.md, TESTING.md
samples/        # Logic App definition and sample usage
tests/          # Unit tests (9 tests covering models and services)
```

## Build and Validation

### Build Commands (run from `src/` directory)

```bash
cd src

# Restore packages (15-20s first time, <1s cached)
dotnet restore

# Build (25s first time, 8s incremental)
dotnet build

# Clean + build (recommended after dependency changes)
dotnet clean && dotnet build
```

**Unit tests exist** (9 tests). Run with `cd tests && dotnet test`.

### Local Development Setup

1. `cp local.settings.json.template local.settings.json`
2. Edit with Azure credentials: `AzureWebJobsStorage` (use `UseDevelopmentStorage=true`), `DocumentIntelligence` endpoint/key (endpoint must end with `/`), `CosmosDb` endpoint/key
3. Start Azurite: `azurite --silent --location /tmp/azurite &`
4. Run: `func start` (requires Azure Functions Core Tools v4; if unavailable due to network restrictions, build with `dotnet build` and deploy to Azure)

## Architecture Quick Reference

- **Trigger**: Azure Storage Queue (`pdf-processing-queue`), input JSON: `{"BlobName": "...", "ContainerName": "...", "IdentifierFieldName": "..."}`
- **Flow**: Queue → Download PDF → Convert pages to images → OCR each page → Aggregate by identifier → Create PDFs → Upload to `processed-documents` container → Save to Cosmos DB
- **Error Handling**: Document Intelligence errors logged but continue processing
- **Entry Point**: `src/Functions/PdfProcessorFunction.cs` orchestrates entire workflow

## Common Issues

| Issue | Solution |
|-------|----------|
| Build fails with missing packages | Run `dotnet restore` first |
| Queue trigger not firing | Verify Azurite running, check `local.settings.json` connection string |
| Document Intelligence errors | Verify endpoint ends with `/`, API key valid |
| Cosmos DB errors | Verify endpoint format and key, ensure database/container exist |
| `local.settings.json` missing | Copy from `.template` file (never commit the actual file) |

## Validation Checklist

1. ✓ `cd src && dotnet clean && dotnet build` succeeds (~30s)
2. ✓ No compiler warnings
3. ✓ Update `local.settings.json.template` if adding settings
4. ✓ Update `docs/` if architecture/deployment changes
5. ✓ New services registered in `Program.cs`

## Critical Constraints

- NO GitHub workflows/CI/CD, tests, or linting - simple POC
- Target: `net8.0`, Runtime: `dotnet-isolated` (required for Functions v4)
- Queue: `pdf-processing-queue`, Output: `processed-documents` container

## Making Changes

- **Services**: Update interface + implementation, register in `Program.cs`
- **Packages**: Add to `.csproj`, run `dotnet restore`
- **Settings**: Update `local.settings.json.template` and docs
- **Models**: Update classes and `README.md` examples

## Key Files

Main: `PdfProcessorFunction.cs`, `Program.cs` (DI), `.csproj` (deps), `local.settings.json.template`
Docs: `ARCHITECTURE.md`, `QUICKSTART.md`, `DEPLOYMENT.md`

## Deployment

`cd src && func azure functionapp publish <app-name>` or see `docs/DEPLOYMENT.md` for Azure setup.

**Trust these verified instructions. Only explore further if incomplete or errors not documented here.**
