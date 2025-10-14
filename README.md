# Document OCR Processor

This is an Azure Functions application that processes PDF files containing multiple documents. The solution uses Azure Document Intelligence to analyze individual pages and then aggregates them into documents based on a configurable identifier field.

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
4. **Page Image Conversion**: The PDF is split into individual page images
5. **Batch OCR Analysis**: Each page image is submitted to Azure Document Intelligence for OCR analysis
6. **Document Aggregation**: Pages are grouped into documents based on a configurable identifier field (e.g., document ID)
7. **PDF Creation**: Individual PDFs are created for each aggregated document
8. **Results Storage**: Individual documents and analysis results are saved to Azure Storage

## Components

### Services

- **PdfToImageService**: Converts PDF pages into individual PNG images for processing
- **DocumentIntelligenceService**: Uses Azure Document Intelligence to extract text, key-value pairs, and tables from document images
- **DocumentAggregatorService**: Groups pages into documents based on identifier fields found in OCR results
- **ImageToPdfService**: Creates PDF documents from collections of page images
- **BlobStorageService**: Handles all blob storage operations for uploading and downloading files

### Models

- **QueueMessage**: Represents the message received from the queue with blob information and identifier field name
- **PageOcrResult**: Contains OCR results for an individual page
- **AggregatedDocument**: Groups pages by their identifier
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

### Document Aggregation by Identifier

The application aggregates pages into documents by extracting an identifier field from each page's OCR results. Pages with the same identifier value are grouped into the same document.

**Key Features:**
- Configurable identifier field name (default: "identifier")
- Automatically groups pages by identifier
- Pages without a valid identifier are treated as individual documents
- Supports any string-based identifier field

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