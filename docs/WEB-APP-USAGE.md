# Web Application Usage Guide

This guide explains how to use the Document OCR Review web application to upload documents, monitor processing operations, and review validated documents.

## Overview

The Document OCR Review web application provides a user-friendly interface for:
- **Uploading PDF documents** for OCR processing
- **Monitoring extraction operations** in real-time
- **Reviewing documents** that have been processed by the OCR system
- **Validating and correcting** extracted data

## Accessing the Application

1. Navigate to the web application URL (provided after deployment)
2. Sign in using your Microsoft Entra ID (Azure AD) credentials
3. You will be redirected to the home page after successful authentication

## Features

### File Upload

The file upload feature allows you to submit PDF documents for OCR processing directly through the web interface.

**To upload documents:**
1. Navigate to the **Upload** page from the navigation menu
2. Click "Choose PDF files to upload (up to 10)" and select one or more PDF documents
   - Maximum 10 files per upload session
   - Each file can be up to 50 MB
3. Review the selected files in the list (showing file name and size)
4. (Optional) Specify a custom identifier field name
   - Default is "identifier"
   - This field is used to group pages into separate documents
   - Pages without this field will be treated as individual single-page documents
5. Click "Upload and Start Extraction"
6. Each file will be uploaded to blob storage sequentially
7. A separate extraction operation will start for each file
8. View the upload results showing success/failure for each file
9. You'll be redirected to the Operations page to monitor all operations

**Multiple File Upload:**
- Upload up to 10 PDF files simultaneously
- Each file is processed independently with its own operation
- Files are uploaded sequentially to ensure reliability
- All operations can be monitored in parallel on the Operations page
- Failed uploads are shown with error messages while successful uploads continue

**Identifier Field:**
The identifier field name tells the OCR system which field to use for grouping pages. For example:
- If you specify "documentId" as the identifier
- Pages with the same documentId value (e.g., "12345") will be grouped into one document
- Pages with different documentId values will be split into separate documents
- Pages where the identifier field is not found, empty, or null are treated as individual single-page documents

### Operations Monitoring

The Operations page shows all document processing operations with real-time status updates.

**Features:**
- **Status filtering**: Filter by NotStarted, Running, Succeeded, Failed, or Cancelled
- **Progress tracking**: See the number of documents processed out of total
- **Auto-refresh**: Enable automatic status updates every 10 seconds
- **Operation details**: View file name, container, creation time, and duration
- **Actions**:
  - **Cancel**: Stop a running operation
  - **Retry**: Restart a failed or completed operation

**Status Indicators:**
- **NotStarted**: Operation queued but not yet started (gray badge)
- **Running**: Currently processing documents (blue badge with progress bar)
- **Succeeded**: Completed successfully (green badge)
- **Failed**: Encountered an error (red badge)
- **Cancelled**: Manually cancelled or timed out (yellow badge)

### Document List

The document list shows all documents that have been processed by the OCR system.

**Filtering:**
- **All Documents**: Shows all documents regardless of status
- **Pending Review**: Shows only documents awaiting review
- **Reviewed**: Shows only documents that have been reviewed

**Information Displayed:**
- Document Number
- Identifier (extracted from OCR)
- Original File Name
- Number of Pages
- Review Status (badge with color coding)
- Assigned To (reviewer)
- Processed Date/Time

**Actions:**
- Click "Review" to open the document review interface

### Document Review Interface

The review interface provides a split view with the PDF on the left and extracted data on the right.

**PDF Viewer:**
- View the processed PDF document (streamed securely from Azure Storage)
- Zoom and navigate through pages
- PDFs are streamed through the web server for security (not publicly accessible)

**Document Information:**
- Document metadata (number, identifier, file name, etc.)
- Review status and history
- Assignment information

**Extracted Data:**
- All fields extracted by OCR are displayed in an enhanced form interface
- Each field shows its **confidence level** as a percentage badge
- Fields are **color-coded** based on confidence (green=high, yellow=medium, red=low)
- **Field types** are displayed (e.g., String, Date, PhoneNumber)
- **Low confidence warnings** appear for fields below 70% confidence
- Each field can be edited inline
- Changes are automatically tracked
- Original OCR confidence scores are preserved

> 📖 **Learn more**: See [Review Page UX Enhancements](REVIEW-PAGE-UX.md) for detailed information about confidence levels and visual indicators.

**Review Actions:**
- **Assign To**: Assign the document to a specific reviewer
- **Mark as Reviewed**: Complete the review and mark the document as reviewed
  - Records reviewer name and timestamp
  - Updates document status
- **Save Changes**: Save any edits to extracted data
- **Back to Documents**: Return to the document list

## Workflow

### Complete Document Processing Workflow

1. **Upload Documents**: 
   - Navigate to the Upload page from the navigation menu
   - Select one or more PDF files (up to 10)
   - Click upload to start processing
   - Each file will trigger a separate extraction operation
2. **Monitor Processing**: 
   - View operation status for all files on the Operations page
   - Enable auto-refresh for real-time updates
   - Wait for operations to complete (status: Succeeded)
3. **Access Document List**: 
   - Navigate to the Documents page when operations complete
   - New documents will appear in the list
4. **Review Documents**:
   - Filter by "Pending Review" to focus on new documents
   - Select and review each document
5. **Complete Review**:
   - Verify and correct extracted data
   - Mark documents as reviewed

### Typical Review Process

1. **Access Document List**: Navigate to `/documents` to see all available documents
2. **Filter by Status**: Select "Pending Review" to focus on documents needing attention
3. **Select Document**: Click "Review" on a document to open it
4. **Review Data**: 
   - Compare the PDF with extracted data
   - Verify accuracy of extracted fields
   - Make corrections as needed
5. **Assign (Optional)**: Assign the document to yourself or another reviewer
6. **Complete Review**: Click "Mark as Reviewed" when verification is complete
7. **Move to Next**: Return to the document list and select the next document

### Editing Extracted Data

1. In the review interface, scroll to the "Extracted Data" section
2. Click in any field to edit the value
3. Type the correct value
4. Changes are tracked automatically
5. Click "Save Changes" to persist your edits

**Data Validation Notes:**
- Field values are stored as text strings
- No automatic format validation is applied
- Review the PDF to ensure accurate transcription
- Empty values are allowed if the field was not found in the original document

### Assignment Workflow

Documents can be assigned to reviewers for tracking purposes:

1. In the review interface, find the "Assign To" field
2. Enter the reviewer's email or name
3. The assignment is saved with the document
4. Assigned documents can be filtered in the document list

## Status Indicators

Documents have color-coded status badges:

- **Pending** (Yellow/Warning): Document awaiting review - default status for newly processed documents
- **Reviewed** (Green/Success): Document has been reviewed and approved by a reviewer

## Best Practices

1. **Review Regularly**: Check for new documents frequently
2. **Verify Against Source**: Always compare extracted data with the PDF
3. **Document Corrections**: When making corrections, ensure accuracy
4. **Use Assignment**: Assign documents to track who is reviewing what
5. **Complete Reviews**: Mark documents as reviewed promptly after verification

## Troubleshooting

### Cannot Sign In
- Verify you have been granted access in Azure AD
- Check that your organization's Azure AD is configured correctly
- Contact your administrator if access is denied

### PDF Not Loading
- Check your network connection
- Verify the web application has access to Azure Storage (uses Managed Identity)
- Ensure the document record has valid ContainerName and BlobName properties
- Check application logs for PDF streaming errors (look for PdfController errors)

### Cannot Save Changes
- Verify you have write access to Cosmos DB
- Check that managed identity permissions are configured
- Review application logs for specific errors

### Missing Documents
- Verify documents have been processed by the Azure Function
- Check Cosmos DB to ensure records were created
- Review function logs for processing errors

## Security

- **Authentication Required**: All pages require Azure AD authentication
- **Secure Communication**: All traffic uses HTTPS
- **Managed Identity**: Application uses managed identity for Azure resource access
- **Role-Based Access**: Access control can be configured in Azure AD

## Support

For issues or questions:
1. Check application logs in Azure Application Insights
2. Review Azure Function logs for processing issues
3. Contact your system administrator
4. Refer to the main [README](../README.md) for architecture details
