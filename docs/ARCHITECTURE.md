# Architecture

## Overview

The Document OCR Processor is built on Azure Functions with a queue-triggered architecture that processes PDF files containing multiple documents. It converts PDF pages to images, performs OCR analysis on each page, and aggregates pages into documents based on identifier fields.

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
        │ PDF to Image │    │   Document   │    │ Azure Doc    │
        │ Service      │    │  Aggregator  │    │ Intelligence │
        │ (PDFtoImage) │    │  Service     │    │ (OCR/Parse)  │
        └──────┬───────┘    └──────┬───────┘    └──────┬───────┘
               │                   │                   │
               └───────────────────┼───────────────────┘
                                   ▼
                          ┌──────────────────┐
                          │ Image to PDF     │
                          │ Service          │
                          │ (PdfSharp)       │
                          └────────┬─────────┘
                                   │
                        ┌──────────┴──────────┐
                        ▼                     ▼
               ┌──────────────────┐  ┌──────────────────┐
               │ Azure Storage    │  │ Cosmos DB        │
               │ Processed Docs   │  │ OCR Results      │
               │ - PDFs           │  │ - Metadata       │
               │ - JSON Results   │  │ - Extracted Data │
               └──────────────────┘  └──────────────────┘
```

## Component Details

### 1. Logic App Workflow
- Monitors email inbox for messages with PDF attachments
- Extracts PDF attachment
- Uploads PDF to Azure Storage Blob container
- Sends message to Storage Queue with blob reference
- Can optionally specify custom identifier field name

### 2. Azure Function (Queue Triggered)
- Triggered by messages in the queue
- Downloads PDF from blob storage
- Orchestrates the document processing workflow:
  1. Convert PDF pages to images
  2. Submit images for OCR analysis
  3. Aggregate pages by identifier (specified in queue message via `IdentifierFieldName`)
  4. Create PDFs from aggregated pages
  5. Upload results to storage

### 3. PDF to Image Service
- Converts each PDF page into a PNG image
- Uses PDFtoImage library with PDFium rendering engine
- Produces high-quality images suitable for OCR processing

### 4. Document Aggregation Strategy
The application groups pages into documents based on identifier fields found in OCR results:

- Extracts identifier field from each page's OCR data
- Groups pages with matching identifiers into the same document
- Configurable field name (default: "identifier")
- Pages without identifiers (field not found, empty, or null) are treated as separate single-page documents using `page_{number}` as the identifier

### 5. Document Intelligence Service
- Uses Azure Document Intelligence (Form Recognizer)
- Analyzes each page image individually
- Extracts:
  - Raw text content
  - Key-value pairs (structured fields including identifiers)
  - Tables
  - Document metadata
- Returns structured JSON data per page

### 6. Image to PDF Service
- Creates PDF documents from collections of page images
- Uses PdfSharp library to create multi-page PDFs
- Preserves page order based on original page numbers

### 7. Cosmos DB Service
- Persists OCR results for each processed document
- Stores document metadata, extracted data, and reference to PDF blob
- Uses document identifier as partition key for efficient querying
- Enables downstream applications (web UI, review workflows) to access processed documents
- Provides queryable storage for analytics and reporting

## Data Flow

1. **Input**: Queue message with blob reference
   
   Default identifier:
   ```json
   {
     "BlobName": "upload-2025-01-10.pdf",
     "ContainerName": "uploaded-pdfs"
   }
   ```
   
   Custom identifier field:
   ```json
   {
     "BlobName": "upload-2025-01-10.pdf",
     "ContainerName": "uploaded-pdfs",
     "IdentifierFieldName": "documentId"
   }
   ```

2. **Processing**:
   - Download PDF from blob storage
   - Convert each page to an image
   - Submit images for OCR analysis (batch processing)
   - Extract identifier field from each page's OCR results
   - Group pages by identifier value
   - Create PDF for each document group
   - Upload results to processed-documents container

3. **Output**: 
   - Individual PDF files: `{original}_doc_{n}.pdf`
   - Result JSON: `{original}_result.json`
   - Cosmos DB records for each document
   
   Result JSON Structure:
   ```json
   {
     "OriginalFileName": "upload-2025-01-10.pdf",
     "TotalDocuments": 2,
     "ProcessedAt": "2025-01-10T12:00:00Z",
     "Documents": [
       {
         "DocumentNumber": 1,
         "PageCount": 3,
         "PageNumbers": [1, 2, 5],
         "Identifier": "4314",
         "ExtractedData": {...},
         "OutputBlobName": "upload-2025-01-10_doc_1.pdf"
       }
     ]
   }
   ```
   
   Cosmos DB Entity Structure:
   ```json
   {
     "id": "unique-guid",
     "documentNumber": 1,
     "originalFileName": "upload-2025-01-10.pdf",
     "identifier": "4314",
     "pageCount": 3,
     "pageNumbers": [1, 2, 5],
     "pdfBlobUrl": "https://storage.blob.core.windows.net/processed-documents/upload-2025-01-10_doc_1.pdf",
     "extractedData": {...},
     "processedAt": "2025-01-10T12:00:00Z",
     "containerName": "processed-documents",
     "blobName": "upload-2025-01-10_doc_1.pdf"
   }
   ```

## Scalability Considerations

- **Queue-based Processing**: Decouples upload from processing
- **Stateless Functions**: Can scale horizontally
- **Blob Storage**: Handles large files efficiently
- **Service Dependencies**: All Azure services are scalable

## Error Handling

- **Document Intelligence Failures**: Logs error and continues with next document
- **Queue Poison Messages**: Automatically moved to poison queue after retry limit
- **Cosmos DB Failures**: Logs error but document processing completes

## Security

- **Managed Identity**: Recommended for production (not implemented in POC)
- **API Keys**: Stored in Azure Key Vault or App Settings
- **Storage Access**: Uses connection strings with appropriate permissions
- **HTTPS**: All API communication over HTTPS
