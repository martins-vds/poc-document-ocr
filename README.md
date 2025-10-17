# Document OCR Processor

This is a complete Azure solution that processes PDF files containing multiple documents using Azure Document Intelligence, with a web application for manual document review. The solution uses Azure Document Intelligence to analyze individual pages, aggregates them into documents based on a configurable identifier field, and stores the results in Azure Cosmos DB for review.

> **🔐 Security:** This solution uses **keyless authentication** with managed identities - no API keys or connection strings in configuration!

## Documentation

- **[⚡ Quick Deploy Guide](QUICK-DEPLOY.md)** - Deploy to Azure in 5 commands (< 20 minutes)
- [Quick Start Guide](docs/QUICKSTART.md) - Get started with local development
- [Architecture](docs/ARCHITECTURE.md) - System design and components
- **[Operations API](docs/OPERATIONS-API.md)** - Asynchronous request-reply API for managing operations
- [Deployment (IaC)](docs/DEPLOYMENT-IAC.md) - **Recommended:** Automated deployment with Bicep and Azure Developer CLI
- [Deployment (Manual)](docs/DEPLOYMENT.md) - Manual Azure deployment instructions
- [Infrastructure as Code](infra/README.md) - IaC technical reference and Bicep modules
- [Bicep Outputs Mapping](docs/BICEP-OUTPUTS-MAPPING.md) - Environment variables mapping for development setup
- [Testing Guide](docs/TESTING.md) - Comprehensive testing documentation

## Architecture

The application follows this workflow:

1. **Email Receipt or Web Upload**: An email with a PDF attachment is received or a PDF is uploaded via the web app
2. **Operations API**: Client calls the Operations API to start processing and receives an operation ID
3. **Logic App Processing** (Email path): A Logic App uploads the PDF to Azure Storage and sends a message to a Storage Queue
4. **Azure Function Trigger**: An Azure Function is triggered by the queue message
5. **Page Image Conversion**: The PDF is split into individual page images
6. **Batch OCR Analysis**: Each page image is submitted to Azure Document Intelligence for OCR analysis
7. **Document Aggregation**: Pages are grouped into documents based on a configurable identifier field (e.g., document ID)
8. **PDF Creation**: Individual PDFs are created for each aggregated document
9. **Results Storage**: Individual documents and analysis results are saved to Azure Storage and Cosmos DB
10. **Status Tracking**: Operation status is updated throughout processing (Running → Succeeded/Failed)
11. **Manual Review**: Reviewers use the web application to verify and correct OCR results

See [Operations API Documentation](docs/OPERATIONS-API.md) for details on the asynchronous request-reply pattern.

## Components

### Azure Function App (DocumentOcrProcessor)

**HTTP Functions (Operations API):**
- **OperationsApi**: RESTful API for managing long-running operations
  - Start, get status, cancel, retry, and list operations
  - Implements asynchronous request-reply pattern
  - See [OPERATIONS-API.md](docs/OPERATIONS-API.md)

**Queue Functions:**
- **PdfProcessorFunction**: Queue-triggered function that processes PDFs
  - Tracks operation progress in Cosmos DB
  - Supports cancellation requests
  - Updates status throughout processing

**Services:**
- **OperationService**: Manages operation lifecycle in Cosmos DB
- **PdfToImageService**: Converts PDF pages into individual PNG images for processing
- **DocumentIntelligenceService**: Uses Azure Document Intelligence to extract text, key-value pairs, and tables from document images
- **DocumentAggregatorService**: Groups pages into documents based on identifier fields found in OCR results
- **ImageToPdfService**: Creates PDF documents from collections of page images
- **BlobStorageService**: Handles all blob storage operations for uploading and downloading files
- **CosmosDbService**: Manages document persistence and queries in Azure Cosmos DB

**Models:**
- **Operation**: Tracks status and progress of long-running operations
- **QueueMessage**: Represents the message received from the queue with blob information and identifier field name
- **PageOcrResult**: Contains OCR results for an individual page
- **AggregatedDocument**: Groups pages by their identifier
- **DocumentResult**: Contains the extracted data and metadata for a single document
- **ProcessingResult**: Contains the complete processing results for all documents in a PDF
- **DocumentOcrEntity**: Represents a document in Cosmos DB with review status and metadata

### Web Application (DocumentOcrWebApp)

A Blazor Server application for manual document review:

**Features:**
- **Authentication**: Microsoft Entra ID (Azure AD) authentication for secure access
- **Document List**: View and filter documents by review status (Pending, Reviewed)
- **Document Review**: View PDF alongside extracted OCR data
- **Data Correction**: Edit and correct OCR results inline
- **Assignment**: Assign documents to specific reviewers
- **Status Tracking**: Track review status and reviewer information

## Prerequisites

- .NET 8.0 SDK
- Azure CLI (for authentication and deployment)
- Azure subscription with the following services:
  - Azure Storage Account
  - Azure Document Intelligence (formerly Form Recognizer)
  - Azure Cosmos DB
  - Azure Functions
  - Azure App Service (for web application)
  - Microsoft Entra ID (Azure AD) app registration for web authentication
- **Azure credentials configured** (Azure CLI login or service principal)

## Authentication

This solution uses **keyless authentication** with:
- **Managed Identities** for Azure resources (Function App, Web App)
- **DefaultAzureCredential** for local development (uses Azure CLI, environment variables, or Visual Studio credentials)
- **No secrets in configuration** - endpoints only!

Benefits:
- ✅ Enhanced security - no credentials to leak
- ✅ Simplified configuration - no connection strings or API keys
- ✅ Automatic credential rotation
- ✅ Works seamlessly between local and Azure environments

## Configuration

Update the `local.settings.json` file with your Azure service endpoints (no keys needed):

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage__accountName": "yourStorageAccount",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "Storage:AccountName": "yourStorageAccount",
        "DocumentIntelligence:Endpoint": "https://your-document-intelligence-endpoint.cognitiveservices.azure.com/",
        "CosmosDb:Endpoint": "https://your-cosmosdb-account.documents.azure.com:443/",
        "CosmosDb:DatabaseName": "DocumentOcrDb",
        "CosmosDb:ContainerName": "ProcessedDocuments"
    }
}
```

**Authentication:** Ensure you're logged in with Azure CLI:
```bash
az login
az account set --subscription "Your-Subscription-Name"
```

Your Azure credentials will be used to authenticate to all services!

### Document Aggregation by Identifier

The application aggregates pages into documents by extracting an identifier field from each page's OCR results. Pages with the same identifier value are grouped into the same document.

**Key Features:**
- Configurable identifier field name (default: "identifier")
- Automatically groups pages by identifier
- Pages without an identifier (field not found, empty, or null) are treated as individual single-page documents
- Supports any string-based identifier field from OCR results

## Queue Message Format

The function expects queue messages in the following JSON format:

### Basic Usage (Default Identifier)
```json
{
    "BlobName": "document.pdf",
    "ContainerName": "uploaded-pdfs"
}
```

This uses the default identifier field name "identifier".

### Custom Identifier Field
```json
{
    "BlobName": "document.pdf",
    "ContainerName": "uploaded-pdfs",
    "IdentifierFieldName": "documentId"
}
```

This tells the processor to look for a field named "documentId" in the OCR results to group pages into documents.

**Example:** If page 1 has `documentId: "4314"` and page 32 also has `documentId: "4314"`, both pages will be grouped into the same document PDF.

## Utility Scripts

A collection of helpful utility scripts is available in the [`utils`](utils/) directory:

- **Configuration Settings Updater** (`utils/update_settings.py`): Python script to update Azure configuration for both Function App and Web App
  - Updates `local.settings.json` for Azure Function and `appsettings.Development.json` for Web App
  - Automatically runs after `azd provision` to configure local development
  - Supports interactive mode or command-line arguments
  - Can read from azd environment variables (`--from-azd-env`)
  - Essential for local development setup and team onboarding
  - Safe for source control (updates Development settings only)
  - No additional dependencies required (uses built-in Python libraries)
- **Base64 File Encoder** (`utils/encode_base64.py`): Python script to encode any file to base64 representation
  - Useful for encoding files for API requests, JSON embedding, and testing base64 file handling
  - No additional dependencies required (uses built-in Python libraries)
- **JSON Schema Generator** (`utils/generate_json_schema.py`): Python script to generate JSON schemas from JSON files
  - Analyzes JSON structure and generates compliant JSON Schema Draft 7 specifications
  - Uses the robust `genson` library for professional schema generation with advanced features
  - Useful for API documentation, data validation, and testing JSON data compliance
  - Requires genson library (`pip install genson`)
- **PDF Splitter** (`utils/split_pdf.py`): Python script to split a multi-page PDF into individual single-page PDF files
  - Useful for creating test data and preparing PDFs for processing
  - Requires pypdf library

See [`utils/README.md`](utils/README.md) for detailed usage instructions for all utility scripts.

## Building and Running

### Local Development

1. Install dependencies:
   ```bash
   cd src/DocumentOcrProcessor
   dotnet restore
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run tests:
   ```bash
   cd ../../tests
   dotnet test
   ```

4. Run locally:
   ```bash
   cd ../src/DocumentOcrProcessor
   func start
   ```

### Deployment to Azure

#### Automated Deployment (Recommended)

Use Bicep and Azure Developer CLI for automated infrastructure provisioning with private networking:

```bash
# Initialize environment
azd auth login
azd env new <environment-name>
azd env set AZURE_LOCATION eastus

# Deploy infrastructure and function code
azd up
```

See [Infrastructure as Code guide](infra/README.md) for detailed instructions.

#### Manual Deployment

Deploy to Azure using Azure Functions Core Tools:

```bash
func azure functionapp publish <your-function-app-name>
```

Or use the utility scripts:

```bash
# Linux/macOS
./infra/scripts/deploy-function.sh <function-app-name>

# Windows PowerShell
./infra/scripts/deploy-function.ps1 -FunctionAppName <function-app-name>
```

See [Manual deployment guide](docs/DEPLOYMENT.md) for step-by-step instructions.

## Output

The function creates the following outputs in the `processed-documents` container:

1. **Individual PDF files**: `{original-name}_doc_{number}.pdf`
2. **Processing result JSON**: `{original-name}_result.json`

The result JSON contains:
- Original file name
- Total number of documents found
- For each document:
  - Document number
  - Page count
  - Extracted data (structured fields from Document Intelligence)
  - Output blob name

## Example Result

```json
{
  "OriginalFileName": "multi-document.pdf",
  "TotalDocuments": 3,
  "ProcessedAt": "2025-01-10T12:00:00Z",
  "Documents": [
    {
      "DocumentNumber": 1,
      "PageCount": 2,
      "ExtractedData": {
        "PageCount": 2,
        "Fields": {
          "fileTkNumber": {
            "type": "String",
            "valueString": "12345",
            "content": "12345",
            "confidence": 0.98
          },
          "accusedName": {
            "type": "String",
            "valueString": "John Doe",
            "content": "John Doe",
            "confidence": 0.95
          },
          "signedOn": {
            "type": "Date",
            "valueDate": "2025-01-10T00:00:00Z",
            "content": "January 10, 2025",
            "confidence": 0.92
          }
        }
      },
      "OutputBlobName": "multi-document_doc_1.pdf"
    }
  ]
}
```

## License

MIT License - see LICENSE file for details