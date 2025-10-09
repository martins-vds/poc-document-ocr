# Sample Files

This directory contains sample files and configurations for the Document OCR Processor.

## Logic App Definition

The `logic-app-definition.json` file contains a sample Logic App workflow that:

1. Monitors an email inbox for new emails with attachments
2. Filters attachments to only process PDF files
3. Uploads each PDF to Azure Blob Storage
4. Sends a message to the processing queue to trigger the Azure Function

### How to Use

1. **Create a Logic App in Azure Portal**:
   ```bash
   az logic workflow create \
     --resource-group rg-document-ocr \
     --name logic-document-ocr \
     --location eastus
   ```

2. **Import the definition**:
   - Open the Logic App in the Azure Portal
   - Go to "Logic app code view"
   - Paste the contents of `logic-app-definition.json`
   - Save the Logic App

3. **Configure connections**:
   - Office 365 connection for email monitoring
   - Azure Blob Storage connection
   - Azure Queue Storage connection

4. **Customize the workflow**:
   - Modify the email folder path
   - Change the blob container name if needed
   - Add additional filters or processing steps

## Sample Queue Message

To manually test the Azure Function, you can send a message to the queue with this format:

```json
{
  "BlobName": "document.pdf",
  "ContainerName": "uploaded-pdfs"
}
```

Example using Azure CLI:

```bash
az storage message put \
  --queue-name pdf-processing-queue \
  --content '{"BlobName":"document.pdf","ContainerName":"uploaded-pdfs"}' \
  --connection-string "<your-connection-string>"
```

## Sample PDF Test Files

For testing purposes, you can use:

- Simple single-page PDFs
- Multi-page PDFs with consistent formatting
- PDFs containing multiple documents with different structures
- PDFs with forms, tables, and key-value pairs

### Creating Test PDFs

You can create test PDFs using various tools:

1. **Microsoft Word**: Create documents and export as PDF
2. **Online converters**: Convert text/images to PDF
3. **PDF libraries**: Generate PDFs programmatically

### Test Scenarios

1. **Single document PDF** (1-5 pages)
   - Tests basic functionality
   - Should create 1 output document

2. **Multi-document PDF** (10+ pages)
   - Tests AI boundary detection
   - Should create multiple output documents

3. **Form-based PDF**
   - Tests key-value pair extraction
   - Should extract form fields

4. **Table-heavy PDF**
   - Tests table extraction
   - Should parse table data

## Expected Output

After processing, you should find in the `processed-documents` container:

1. **Split PDFs**: `{original-name}_doc_{n}.pdf`
2. **Result JSON**: `{original-name}_result.json`

Example result JSON structure:

```json
{
  "OriginalFileName": "multi-doc.pdf",
  "TotalDocuments": 2,
  "ProcessedAt": "2025-01-10T12:00:00Z",
  "Documents": [
    {
      "DocumentNumber": 1,
      "PageCount": 3,
      "ExtractedData": {
        "PageCount": 3,
        "Content": "Full text content...",
        "KeyValuePairs": {
          "Name": "John Doe",
          "Date": "2025-01-10"
        },
        "Tables": []
      },
      "OutputBlobName": "multi-doc_doc_1.pdf"
    },
    {
      "DocumentNumber": 2,
      "PageCount": 2,
      "ExtractedData": {
        "PageCount": 2,
        "Content": "Full text content...",
        "KeyValuePairs": {},
        "Tables": [
          {
            "RowCount": 5,
            "ColumnCount": 3,
            "Cells": [...]
          }
        ]
      },
      "OutputBlobName": "multi-doc_doc_2.pdf"
    }
  ]
}
```

## Notes

- Replace placeholder values with your actual Azure resource names
- Ensure all connections are properly authenticated
- Test with small PDFs first before processing large files
- Monitor Application Insights for detailed execution logs
