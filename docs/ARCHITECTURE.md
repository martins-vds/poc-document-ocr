# Architecture

## Overview

The Document OCR Processor is a complete Azure solution that combines automated document processing with manual review capabilities. The system uses Azure Functions for queue-triggered OCR processing and a Blazor web application for human review and validation of extracted data. Documents are processed through Azure Document Intelligence, stored in Cosmos DB, and made available for review through a secure web interface.

## Schema-driven consolidation (feature 001-document-schema-aggregation)

The processor pipeline is:

```
DocumentAggregatorService  (forward-fill identifier per FR-020)
  → DocumentSchemaMapperService  (per-field merge: highest-confidence / concat / signature → bool)
  → CosmosDbService.CreateDocumentAsync  (one record per fileTkNumber, 13 SchemaFields, ETag for optimistic concurrency)
```

The Blazor WebApp consumes:

```
ReviewController
  → IDocumentLockService  (24h opportunistic stale-checkout release)
  → IDocumentReviewService  (per-field state machine + Pending → Reviewed transition)
  → ICurrentUserService  (UPN from authenticated principal)
```


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
               └──────────────────┘  └────────┬─────────┘
                                              │
                                              ▼
                                     ┌──────────────────┐
                                     │   Web App        │
                                     │  (Blazor Server) │
                                     │  - Document List │
                                     │  - Review UI     │
                                     │  - PDF Viewer    │
                                     │  - Entra ID Auth │
                                     └──────────────────┘
```

### Operations API (Asynchronous Request-Reply Pattern)

```
┌──────────────┐
│ Client       │
│ (Web/Email)  │
└──────┬───────┘
       │ POST /api/operations
       ▼
┌────────────────────┐     ┌──────────────────┐
│ Operations API     │────▶│ Cosmos DB        │
│ - Start Operation  │     │ Operations       │
│ - Get Status       │◄────│ Container        │
│ - Cancel           │     └──────────────────┘
│ - Retry            │              │
└────────┬───────────┘              │
       │ 202 Accepted               │
       │ Location: /api/ops/{id}    │
       ▼                            │
┌──────────────┐                    │
│ Queue Msg    │                    │
│ + OperationId│                    │
└──────┬───────┘                    │
       │                            │
       ▼                            ▼
┌────────────────────┐     ┌──────────────────┐
│ PdfProcessor       │────▶│ Updates Status   │
│ - Tracks OpId      │     │ - Running        │
│ - Updates Progress │     │ - Succeeded      │
│ - Handles Cancel   │     │ - Failed         │
└────────────────────┘     └──────────────────┘
```

## Component Details

### 0. Operations API (NEW)

A RESTful HTTP API that implements the [Asynchronous Request-Reply pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/async-request-reply) for managing long-running document processing operations.

**Features:**
- **Start Operation**: Initiates document processing and returns operation ID
- **Get Status**: Polls operation status with progress information
- **Cancel Operation**: Cancels running or pending operations
- **Retry Operation**: Retries failed operations or restarts completed ones
- **List Operations**: Lists all operations with optional filtering

**Endpoints:**
- `POST /api/operations` - Start new operation
- `GET /api/operations/{id}` - Get operation status
- `POST /api/operations/{id}/cancel` - Cancel operation
- `POST /api/operations/{id}/retry` - Retry/restart operation
- `GET /api/operations` - List all operations

**Status Flow:**
```
NotStarted → Running → Succeeded/Failed/Cancelled
```

**Storage:**
- Operations tracked in Cosmos DB `Operations` container
- Partition key: `/id`
- Enables async polling and history tracking

See [OPERATIONS-API.md](OPERATIONS-API.md) for complete API documentation.

### 1. Logic App Workflow
- Monitors email inbox for messages with PDF attachments
- Extracts PDF attachment
- Uploads PDF to Azure Storage Blob container
- Sends message to Storage Queue with blob reference

### 2. Azure Function (Queue Triggered)
- Triggered by messages in the queue
- Downloads PDF from blob storage
- Orchestrates the document processing workflow:
  1. Convert PDF pages to images
  2. Submit images for OCR analysis
  3. Aggregate pages by identifier (field name configured via the `DocumentProcessing:IdentifierFieldName` app setting)
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
- Field name is configured via the `DocumentProcessing:IdentifierFieldName` app setting (default: "identifier")
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

   ```json
   {
     "BlobName": "upload-2025-01-10.pdf",
     "ContainerName": "uploaded-pdfs",
     "PageRange": "3-12, 15"
   }
   ```

   - `PageRange` is **optional**. When omitted, null, or whitespace, the worker processes every page (back-compat with messages enqueued before feature 002).
   - When supplied, the parser ([`PageSelection`](../src/DocumentOcr.Common/Models/PageSelection.cs)) restricts the OCR loop to the chosen pages. Excluded image streams are disposed up front; document-local citations remain `1..N` (FR-011). See [contracts/queue-message.md](../specs/002-upload-page-range-selection/contracts/queue-message.md).

   The identifier field name used during aggregation is read from the Function App's `DocumentProcessing:IdentifierFieldName` setting and is not part of the message.

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
     "blobName": "upload-2025-01-10_doc_1.pdf",
     "reviewStatus": "Pending",
     "assignedTo": null,
     "reviewedBy": null,
     "reviewedAt": null
   }
   ```

### 6. Web Application (Manual Review)

A Blazor Server application that provides a user interface for reviewing and correcting OCR results.

**Features:**
- **Authentication**: Microsoft Entra ID (Azure AD) integration for secure access
- **Document List View**: Browse documents with filtering by review status
- **Document Review Interface**: 
  - Side-by-side PDF viewer and extracted data display
  - Inline editing of OCR results
  - Document assignment to reviewers
  - Status tracking (Pending, Reviewed)
- **Data Correction**: Edit extracted field values with validation
- **Review Workflow**: Mark documents as reviewed with reviewer tracking

**Technology Stack:**
- Blazor Server (.NET 8)
- Microsoft.Identity.Web for authentication
- Azure Cosmos DB SDK for data access
- Azure Storage Blobs SDK for PDF access
- Bootstrap 5 for UI styling

**Access Control:**
- All pages require authentication via Entra ID
- User identity tracked for assignments and reviews
- Role-based access can be extended as needed

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
