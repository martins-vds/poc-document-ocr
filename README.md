# Document OCR Processor

This is an Azure Functions application that processes PDF files containing multiple documents. The solution uses Azure AI Foundry and Azure Document Intelligence to split PDFs into individual documents and extract information from them.

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
        "DocumentIntelligence:ApiKey": "your-document-intelligence-api-key"
    }
}
```

## Queue Message Format

The function expects queue messages in the following JSON format:

```json
{
    "BlobName": "document.pdf",
    "ContainerName": "uploaded-pdfs"
}
```

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

3. Run locally:
   ```bash
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