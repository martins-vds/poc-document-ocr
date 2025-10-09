# Architecture

## Overview

The Document OCR Processor is built on Azure Functions with a queue-triggered architecture that processes PDF files containing multiple documents.

## Architecture Diagram

```
┌─────────────────┐
│   Email with    │
│   PDF Attached  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Logic App     │
│  - Upload PDF   │
│  - Queue Msg    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐         ┌──────────────────┐
│  Azure Storage  │────────▶│ Storage Queue    │
│  Blob Container │         │ (pdf-processing) │
└─────────────────┘         └────────┬─────────┘
                                     │
                                     ▼
                            ┌──────────────────┐
                            │ Azure Function   │
                            │ (Queue Trigger)  │
                            └────────┬─────────┘
                                     │
                ┌────────────────────┼────────────────────┐
                ▼                    ▼                    ▼
        ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
        │ Azure AI     │    │ PDF Splitter │    │ Azure Doc    │
        │ Foundry      │    │ Service      │    │ Intelligence │
        │ (Boundaries) │    │ (PdfSharp)   │    │ (OCR/Parse)  │
        └──────┬───────┘    └──────┬───────┘    └──────┬───────┘
               │                   │                   │
               └───────────────────┼───────────────────┘
                                   ▼
                          ┌──────────────────┐
                          │ Azure Storage    │
                          │ Processed Docs   │
                          │ - PDFs           │
                          │ - JSON Results   │
                          └──────────────────┘
```

## Component Details

### 1. Logic App Workflow
- Monitors email inbox for messages with PDF attachments
- Extracts PDF attachment
- Uploads PDF to Azure Storage Blob container
- Sends message to Storage Queue with blob reference
- Can optionally include manual page boundaries in queue message

### 2. Azure Function (Queue Triggered)
- Triggered by messages in the queue
- Downloads PDF from blob storage
- Orchestrates the document processing workflow
- Uses configured strategy for boundary detection
- Saves results back to blob storage

### 3. Document Boundary Detection Strategies
The application supports two strategies for detecting document boundaries:

#### AI Boundary Detection Strategy (Default)
- Analyzes PDF structure to detect document boundaries
- Uses GPT models via Azure AI Foundry to intelligently identify where documents start
- Returns page numbers where new documents begin
- Fallback: treats PDF as single document if AI fails

#### Manual Boundary Detection Strategy
- Uses page boundaries provided in the queue message
- Validates page numbers against PDF page count
- No external service calls required
- Useful when document structure is known in advance

### 4. PDF Splitter Service
- Uses PdfSharp library to manipulate PDF files
- Splits PDF at boundaries detected by the configured strategy
- Creates individual PDF files for each document
- Accepts optional manual boundaries from queue message

### 5. Document Intelligence Service
- Uses Azure Document Intelligence (Form Recognizer)
- Extracts:
  - Raw text content
  - Key-value pairs (structured fields)
  - Tables
  - Document metadata
- Returns structured JSON data

## Data Flow

1. **Input**: Queue message with blob reference
   
   AI-Based Detection:
   ```json
   {
     "BlobName": "upload-2025-01-10.pdf",
     "ContainerName": "uploaded-pdfs"
   }
   ```
   
   Manual Detection:
   ```json
   {
     "BlobName": "upload-2025-01-10.pdf",
     "ContainerName": "uploaded-pdfs",
     "UseManualDetection": true,
     "ManualBoundaries": [1, 5, 10]
   }
   ```

2. **Processing**:
   - Download PDF from blob storage
   - AI analysis to find document boundaries
   - Split PDF into individual documents
   - Analyze each document with Document Intelligence
   - Upload results to processed-documents container

3. **Output**: 
   - Individual PDF files: `{original}_doc_{n}.pdf`
   - Result JSON: `{original}_result.json`
   ```json
   {
     "OriginalFileName": "upload-2025-01-10.pdf",
     "TotalDocuments": 3,
     "ProcessedAt": "2025-01-10T12:00:00Z",
     "Documents": [...]
   }
   ```

## Scalability Considerations

- **Queue-based Processing**: Decouples upload from processing
- **Stateless Functions**: Can scale horizontally
- **Blob Storage**: Handles large files efficiently
- **Service Dependencies**: All Azure services are scalable

## Error Handling

- **AI Foundry Failures**: Falls back to treating PDF as single document
- **Document Intelligence Failures**: Logs error and continues with next document
- **Queue Poison Messages**: Automatically moved to poison queue after retry limit

## Security

- **Managed Identity**: Recommended for production (not implemented in POC)
- **API Keys**: Stored in Azure Key Vault or App Settings
- **Storage Access**: Uses connection strings with appropriate permissions
- **HTTPS**: All API communication over HTTPS
