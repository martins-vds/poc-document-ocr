# Document OCR Processor

This is an Azure Functions application that processes PDF files containing multiple documents. The solution uses Azure AI Foundry and Azure Document Intelligence to split PDFs into individual documents and extract information from them.

## Documentation

- [Quick Start Guide](docs/QUICKSTART.md) - Get started with local development
- [Architecture](docs/ARCHITECTURE.md) - System design and components
- [Deployment](docs/DEPLOYMENT.md) - Azure deployment instructions
- [Testing Guide](docs/TESTING.md) - Comprehensive testing documentation

## Architecture

The application follows this workflow:

1. **Email Receipt**: An email with a PDF attachment is received
2. **Logic App Processing**: A Logic App uploads the PDF to Azure Storage and sends a message to a Storage Queue
3. **Azure Function Trigger**: An Azure Function is triggered by the queue message
4. **Document Splitting**: The function uses Azure AI Foundry to detect document boundaries within the PDF
5. **PDF Splitting**: The PDF is split into individual documents based on detected boundaries
6. **Document Analysis**: Each document is analyzed using Azure Document Intelligence to extract information
7. **Results Storage**: Individual documents and analysis results are saved to Azure Storage

## Components

### Services

- **PdfSplitterService**: Splits multi-document PDFs into individual documents using AI-detected boundaries
- **AiFoundryService**: Uses Azure AI Foundry to intelligently detect where documents begin in a PDF
- **DocumentIntelligenceService**: Uses Azure Document Intelligence to extract text, key-value pairs, and tables from documents
- **BlobStorageService**: Handles all blob storage operations for uploading and downloading files

### Models

- **QueueMessage**: Represents the message received from the queue with blob information
- **DocumentResult**: Contains the extracted data and metadata for a single document
- **ProcessingResult**: Contains the complete processing results for all documents in a PDF

## Prerequisites

- .NET 8.0 SDK
- Azure subscription with the following services:
  - Azure Storage Account
  - Azure AI Foundry (Azure OpenAI)
  - Azure Document Intelligence (formerly Form Recognizer)
  - Azure Functions

## Configuration

Update the `local.settings.json` file with your Azure service endpoints and API keys:

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=yourStorageAccount;AccountKey=yourKey;EndpointSuffix=core.windows.net",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "AzureAiFoundry:Endpoint": "https://your-ai-foundry-endpoint.openai.azure.com",
        "AzureAiFoundry:ApiKey": "your-ai-foundry-api-key",
        "DocumentIntelligence:Endpoint": "https://your-document-intelligence-endpoint.cognitiveservices.azure.com/",
        "DocumentIntelligence:ApiKey": "your-document-intelligence-api-key",
        "DocumentBoundaryDetection:UseManual": "false"
    }
}
```

### Document Boundary Detection Strategies

The application supports two strategies for detecting document boundaries:

1. **AI-Based Detection (Default)**: Uses Azure AI Foundry to automatically detect where documents begin based on content analysis
   - Set `"DocumentBoundaryDetection:UseManual": "false"` (or omit the setting)
   - Requires Azure AI Foundry configuration

2. **Manual Detection**: Allows you to implement custom boundary detection logic
   - Set `"DocumentBoundaryDetection:UseManual": "true"`
   - Set `UseManualDetection: true` in the queue message
   - Extend `ManualBoundaryDetectionStrategy` class to implement your own detection logic
   - Does not require Azure AI Foundry configuration

## Queue Message Format

The function expects queue messages in the following JSON format:

### AI-Based Detection (Default)
```json
{
    "BlobName": "document.pdf",
    "ContainerName": "uploaded-pdfs"
}
```

### Manual Boundary Detection
```json
{
    "BlobName": "document.pdf",
    "ContainerName": "uploaded-pdfs",
    "UseManualDetection": true
}
```

When `UseManualDetection` is set to `true`, the system will use the `ManualBoundaryDetectionStrategy`. By default, this treats the PDF as a single document. You can extend this class to implement your own custom boundary detection logic based on your specific requirements.

## Utility Scripts

A collection of helpful utility scripts is available in the [`utils`](utils/) directory:

- **Base64 File Encoder** (`utils/encode_base64.py`): Python script to encode any file to base64 representation
  - Useful for encoding files for API requests, JSON embedding, and testing base64 file handling
  - No additional dependencies required (uses built-in Python libraries)
- **PDF Splitter** (`utils/split_pdf.py`): Python script to split a multi-page PDF into individual single-page PDF files
  - Useful for creating test data and preparing PDFs for processing
  - Requires pypdf library

See [`utils/README.md`](utils/README.md) for detailed usage instructions for all utility scripts.

## Building and Running

### Local Development

1. Install dependencies:
   ```bash
   cd src
   dotnet restore
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run tests:
   ```bash
   cd ../tests
   dotnet test
   ```

4. Run locally:
   ```bash
   cd ../src
   func start
   ```

### Deployment

Deploy to Azure using Azure Functions Core Tools:

```bash
func azure functionapp publish <your-function-app-name>
```

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
  - Extracted data (text, key-value pairs, tables)
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
        "Content": "...",
        "KeyValuePairs": {
          "Invoice Number": "INV-001",
          "Date": "2025-01-10"
        },
        "Tables": [...]
      },
      "OutputBlobName": "multi-document_doc_1.pdf"
    }
  ]
}
```

## License

MIT License - see LICENSE file for details