# Copilot Agent Instructions for Document OCR Processor

## Repository Overview

This is an **Azure Functions application** (proof-of-concept) that processes PDF files containing multiple documents. The application uses Azure AI Foundry (Azure OpenAI) to intelligently detect document boundaries within multi-document PDFs, splits them into individual PDFs, and extracts structured data using Azure Document Intelligence (formerly Form Recognizer).

**Repository Size**: Small (~24 files, no tests)
**Primary Language**: C# (.NET 8.0)
**Framework**: Azure Functions v4 (dotnet-isolated runtime)
**Key Dependencies**: PdfSharpCore, Azure.AI.FormRecognizer, Azure.AI.Inference, Azure.Storage.Blobs

## Project Structure

```
/
├── .github/                 # GitHub configuration (this file)
├── docs/                    # Documentation
│   ├── ARCHITECTURE.md      # System architecture and data flow
│   ├── DEPLOYMENT.md        # Azure deployment guide
│   └── QUICKSTART.md        # Local development setup guide
├── samples/                 # Sample files and configurations
│   ├── logic-app-definition.json  # Logic App workflow example
│   └── README.md            # Sample usage documentation
└── src/                     # Main source code (working directory for builds)
    ├── Functions/           # Azure Function definitions
    │   └── PdfProcessorFunction.cs  # Queue-triggered function (entry point)
    ├── Models/              # Data models
    │   ├── QueueMessage.cs          # Input message format
    │   ├── DocumentResult.cs        # Per-document result
    │   └── ProcessingResult.cs      # Complete processing output
    ├── Services/            # Business logic services
    │   ├── IPdfSplitterService.cs           # Interface
    │   ├── PdfSplitterService.cs            # PDF manipulation (61 lines)
    │   ├── IAiFoundryService.cs             # Interface
    │   ├── AiFoundryService.cs              # AI boundary detection (95 lines)
    │   ├── IDocumentIntelligenceService.cs  # Interface
    │   └── DocumentIntelligenceService.cs   # OCR extraction (86 lines)
    ├── Program.cs                 # Dependency injection setup
    ├── DocumentOcrProcessor.csproj # .NET project file
    ├── host.json                  # Functions runtime config
    └── local.settings.json.template # Configuration template
```

## Build and Validation

### Prerequisites

- **.NET 8.0 SDK** (tested with 9.0.305 but targets net8.0)
- **Azure Functions Core Tools v4** (optional for local testing; requires network access to cdn.functions.azure.com)
- **Azure Storage Emulator or Azurite** (for local development)
- **Azure subscription** with Azure OpenAI and Document Intelligence services (for runtime)

### Build Commands

**ALWAYS run commands from the `/src` directory, not the repository root.**

All build commands work reliably and complete quickly (~15-30 seconds):

```bash
cd src

# 1. Restore packages (15-20 seconds first time, <1 second cached)
dotnet restore

# 2. Build the project (~25 seconds first time, ~8 seconds incremental)
dotnet build

# 3. Clean build artifacts (~0.5 seconds)
dotnet clean

# 4. Clean + build sequence (recommended after dependency changes)
dotnet clean && dotnet build
```

**Build Notes:**
- Restore time: ~15-20 seconds on first run, <1 second when cached
- Build time: ~25 seconds on first build, ~8 seconds on incremental builds
- No additional build steps are required; the Azure Functions SDK handles metadata generation automatically
- Build output: `bin/Debug/net8.0/DocumentOcrProcessor.dll`

### Testing

**There are NO unit tests in this project.** Running `dotnet test` will succeed with the message "ShowInfoMessageIfProjectHasNoIsTestProjectProperty" but does not execute any tests.

If adding tests in the future, create a separate test project following the standard pattern: `DocumentOcrProcessor.Tests.csproj`.

### Local Development Setup

To run the function locally, you need:

1. **Copy configuration template:**
   ```bash
   cd src
   cp local.settings.json.template local.settings.json
   ```

2. **Edit `local.settings.json`** with real Azure service credentials:
   - `AzureWebJobsStorage`: Use `UseDevelopmentStorage=true` for Azurite, or a real connection string
   - `AzureAiFoundry:Endpoint`: Your Azure OpenAI endpoint (must have a deployed chat model)
   - `AzureAiFoundry:ApiKey`: Your Azure OpenAI API key
   - `DocumentIntelligence:Endpoint`: Your Document Intelligence endpoint (must end with `/`)
   - `DocumentIntelligence:ApiKey`: Your Document Intelligence API key

3. **Start Azure Storage Emulator (Azurite recommended):**
   ```bash
   # Install globally if needed
   npm install -g azurite
   
   # Start in background
   azurite --silent --location /tmp/azurite --debug /tmp/azurite/debug.log &
   ```

4. **Run the function locally (requires Azure Functions Core Tools):**
   ```bash
   cd src
   func start
   ```
   
   **Note:** `func` may not be available in restricted environments. The build process itself does not require it.

### Running Without Azure Functions Core Tools

If `func` is not available (network restrictions prevent installation), you can still:
- Build and validate the code with `dotnet build`
- Deploy directly to Azure with proper deployment scripts
- Test via Azure portal after deployment

## Architecture Notes

### Function Trigger
- **Trigger Type**: Azure Storage Queue (`pdf-processing-queue`)
- **Input Format**: JSON with `BlobName` and `ContainerName` properties
- **Connection String**: Uses `AzureWebJobsStorage` setting

### Data Flow
1. Queue message received with blob reference
2. PDF downloaded from blob storage
3. AI Foundry analyzes PDF structure to detect document boundaries
4. PDF split into individual documents using detected boundaries
5. Each document processed by Document Intelligence to extract data
6. Individual PDFs and result JSON uploaded to `processed-documents` container

### Output Containers
- **Input**: `uploaded-pdfs` (or container specified in queue message)
- **Output**: `processed-documents` (auto-created by function)

### Error Handling
- AI Foundry failures: Falls back to treating PDF as single document
- Document Intelligence failures: Logs error and continues with next document
- Queue poison messages: Automatically moved to poison queue after retry limit

## Common Issues and Workarounds

### Build Issues
- **Issue**: Build fails with missing packages
  - **Solution**: Run `dotnet restore` before building
  
### Local Development Issues
- **Issue**: "Queue trigger not firing"
  - **Solution**: Verify Azurite is running, check connection string in `local.settings.json`, ensure queue name is `pdf-processing-queue`

- **Issue**: "AI Foundry errors"
  - **Solution**: Verify endpoint URL format, check API key validity, ensure a chat model is deployed in Azure OpenAI

- **Issue**: "Document Intelligence errors"  
  - **Solution**: Verify endpoint URL ends with `/`, check API key validity, ensure service is accessible

### Configuration Issues
- **Issue**: `local.settings.json` missing
  - **Solution**: Copy from `local.settings.json.template` and fill in real values
  - **Note**: This file is in `.gitignore` and should never be committed

## Key Files to Understand

### Entry Point
- `src/Functions/PdfProcessorFunction.cs` - The queue-triggered function; orchestrates the entire workflow

### Service Layer
- `src/Services/PdfSplitterService.cs` - Uses PdfSharpCore to split PDFs at AI-detected boundaries
- `src/Services/AiFoundryService.cs` - Calls Azure OpenAI to analyze PDF structure and detect document starts
- `src/Services/DocumentIntelligenceService.cs` - Uses Azure Document Intelligence to extract text, key-value pairs, and tables

### Configuration
- `src/host.json` - Functions runtime configuration (Application Insights settings)
- `src/local.settings.json.template` - Template for local development configuration
- `src/DocumentOcrProcessor.csproj` - Project file with all NuGet package references

### Models
- `src/Models/QueueMessage.cs` - Input message format
- `src/Models/ProcessingResult.cs` - Complete processing output with all documents
- `src/Models/DocumentResult.cs` - Per-document extraction result

## Deployment

**Do not deploy from local builds.** Use Azure Functions Core Tools or CI/CD pipeline:

```bash
cd src
func azure functionapp publish <your-function-app-name>
```

See `docs/DEPLOYMENT.md` for complete Azure resource setup including:
- Storage Account creation
- Azure OpenAI (AI Foundry) setup
- Document Intelligence service setup  
- Function App creation and configuration
- Application Insights for monitoring

## Validation Checklist

Before submitting changes:

1. **Build succeeds**: `cd src && dotnet clean && dotnet build` (should complete in ~30 seconds)
2. **No new warnings**: Check build output for compiler warnings
3. **Configuration template updated**: If adding new settings, update `local.settings.json.template`
4. **Documentation updated**: Update `docs/` files if architecture or deployment changes
5. **Service interfaces preserved**: Changes to service interfaces require updates to implementations
6. **Dependency injection registered**: New services must be registered in `Program.cs`

## Important Constraints

- **NO GitHub workflows or CI/CD pipelines exist** - this is a simple POC project
- **NO automated tests** - manual testing via Azure portal or local function execution required
- **NO linting configuration** - follow standard C# coding conventions
- **local.settings.json is git-ignored** - always use the .template file as reference
- **Target Framework**: Must remain `net8.0` for Azure Functions v4 compatibility
- **Functions Runtime**: Must remain `dotnet-isolated` for proper worker process isolation

## Making Code Changes

When modifying code:

1. **Service changes**: Update both interface and implementation; ensure DI registration in `Program.cs`
2. **New NuGet packages**: Add to `DocumentOcrProcessor.csproj` and run `dotnet restore`
3. **New Azure services**: Document configuration in `local.settings.json.template` and update `docs/QUICKSTART.md`
4. **Function changes**: Queue trigger name and connection string must match existing infrastructure
5. **Model changes**: Update both C# models and documentation examples in `README.md`

## Trust These Instructions

These instructions have been verified by:
- Building the project successfully from clean state
- Testing clean + build workflows
- Validating all documented paths and file locations
- Confirming build timings and command sequences

**Only perform additional exploration if:**
- These instructions are incomplete for your specific task
- You encounter errors not documented here
- You need to understand implementation details not covered here

For questions about architecture, see `docs/ARCHITECTURE.md`. For local setup details, see `docs/QUICKSTART.md`. For Azure deployment, see `docs/DEPLOYMENT.md`.
