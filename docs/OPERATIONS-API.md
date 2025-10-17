# Operations API

The Operations API provides asynchronous request-reply pattern support for long-running document processing operations. This enables users to track status, cancel running operations, and retry failed operations.

## Overview

The Operations API follows the [Asynchronous Request-Reply pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/async-request-reply) recommended by Microsoft Azure Architecture Center.

**Key Features:**
- Start document processing operations
- Poll for operation status
- Cancel running operations
- Retry failed or restart completed operations
- List all operations with optional filtering

## Architecture

```
┌─────────────────┐
│  Client Request │
│  (Email/WebApp) │
└────────┬────────┘
         │
         ▼
┌─────────────────────────┐
│ POST /api/operations    │──────┐
│ Start Operation         │      │
└────────┬────────────────┘      │
         │ 202 Accepted          │ Creates Operation
         │ Location: /api/       │ Record in Cosmos DB
         │  operations/{id}      │
         ▼                       │
┌─────────────────────────┐     │
│ Client Polls Status     │◄────┘
│ GET /api/operations/{id}│
└────────┬────────────────┘
         │ While Running:
         │  202 Accepted
         │  Retry-After: 10
         │
         ▼ When Complete:
         │  200 OK
┌────────┴────────────────┐
│ Processing Result       │
│ with documents          │
└─────────────────────────┘
```

## API Endpoints

### 1. Start Operation

**POST** `/api/operations`

Starts a new document processing operation by uploading a PDF for processing.

**Request Body:**
```json
{
  "blobName": "upload-2025-01-10.pdf",
  "containerName": "uploaded-pdfs",
  "identifierFieldName": "identifier"  // optional, defaults to "identifier"
}
```

**Response:** `202 Accepted`
```json
{
  "operationId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "NotStarted",
  "statusQueryGetUri": "https://<function-app>.azurewebsites.net/api/operations/123e4567-e89b-12d3-a456-426614174000"
}
```

**Response Headers:**
- `Location`: URL to poll for operation status

**Example:**
```bash
curl -X POST "https://<function-app>.azurewebsites.net/api/operations?code=<function-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "blobName": "sample.pdf",
    "containerName": "uploaded-pdfs"
  }'
```

---

### 2. Get Operation Status

**GET** `/api/operations/{operationId}`

Retrieves the current status of a processing operation.

**Response (While Running):** `202 Accepted`
```json
{
  "operationId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "Running",
  "blobName": "upload-2025-01-10.pdf",
  "containerName": "uploaded-pdfs",
  "createdAt": "2025-01-10T10:00:00Z",
  "startedAt": "2025-01-10T10:00:05Z",
  "completedAt": null,
  "processedDocuments": 3,
  "totalDocuments": 10,
  "resultBlobName": null,
  "error": null,
  "cancelRequested": false
}
```

**Response Headers (While Running):**
- `Retry-After`: 10 (seconds to wait before next poll)

**Response (Completed):** `200 OK`
```json
{
  "operationId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "Succeeded",
  "blobName": "upload-2025-01-10.pdf",
  "containerName": "uploaded-pdfs",
  "createdAt": "2025-01-10T10:00:00Z",
  "startedAt": "2025-01-10T10:00:05Z",
  "completedAt": "2025-01-10T10:05:30Z",
  "processedDocuments": 10,
  "totalDocuments": 10,
  "resultBlobName": "upload-2025-01-10_result.json",
  "error": null,
  "cancelRequested": false
}
```

**Response (Failed):** `500 Internal Server Error`
```json
{
  "operationId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "Failed",
  "blobName": "upload-2025-01-10.pdf",
  "containerName": "uploaded-pdfs",
  "createdAt": "2025-01-10T10:00:00Z",
  "startedAt": "2025-01-10T10:00:05Z",
  "completedAt": "2025-01-10T10:02:15Z",
  "processedDocuments": 2,
  "totalDocuments": 10,
  "resultBlobName": null,
  "error": "Document Intelligence service unavailable",
  "cancelRequested": false
}
```

**Example:**
```bash
curl "https://<function-app>.azurewebsites.net/api/operations/123e4567-e89b-12d3-a456-426614174000?code=<function-key>"
```

---

### 3. Cancel Operation

**POST** `/api/operations/{operationId}/cancel`

Cancels a running or pending operation.

**Response:** `200 OK`
```json
{
  "operationId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "Cancelled",
  "message": "Operation cancelled successfully"
}
```

**Error Response:** `400 Bad Request`
```json
"Cannot cancel operation in Succeeded status"
```

**Notes:**
- Operations in `NotStarted` status are immediately cancelled
- Operations in `Running` status will be cancelled at the next checkpoint
- Cannot cancel operations that are already `Succeeded`, `Failed`, or `Cancelled`

**Example:**
```bash
curl -X POST "https://<function-app>.azurewebsites.net/api/operations/123e4567-e89b-12d3-a456-426614174000/cancel?code=<function-key>"
```

---

### 4. Retry Operation

**POST** `/api/operations/{operationId}/retry`

Creates a new operation with the same parameters as a failed, cancelled, or completed operation.

**Response:** `202 Accepted`
```json
{
  "operationId": "456e7890-e89b-12d3-a456-426614174111",
  "status": "NotStarted",
  "statusQueryGetUri": "https://<function-app>.azurewebsites.net/api/operations/456e7890-e89b-12d3-a456-426614174111",
  "message": "Operation retry started"
}
```

**Response Headers:**
- `Location`: URL to poll for new operation status

**Error Response:** `400 Bad Request`
```json
"Cannot retry operation in Running status. Only Failed, Cancelled, or Succeeded operations can be retried."
```

**Notes:**
- Creates a **new** operation (new ID)
- Original operation remains unchanged
- Can retry `Failed`, `Cancelled`, or `Succeeded` operations
- Cannot retry operations that are still `Running` or `NotStarted`

**Example:**
```bash
curl -X POST "https://<function-app>.azurewebsites.net/api/operations/123e4567-e89b-12d3-a456-426614174000/retry?code=<function-key>"
```

---

### 5. List Operations

**GET** `/api/operations`

Lists all operations with optional filtering.

**Query Parameters:**
- `status` (optional): Filter by operation status (`NotStarted`, `Running`, `Succeeded`, `Failed`, `Cancelled`)
- `maxItems` (optional): Maximum number of operations to return

**Response:** `200 OK`
```json
{
  "operations": [
    {
      "operationId": "123e4567-e89b-12d3-a456-426614174000",
      "status": "Succeeded",
      "blobName": "upload-2025-01-10.pdf",
      "containerName": "uploaded-pdfs",
      "createdAt": "2025-01-10T10:00:00Z",
      "startedAt": "2025-01-10T10:00:05Z",
      "completedAt": "2025-01-10T10:05:30Z",
      "processedDocuments": 10,
      "totalDocuments": 10,
      "resultBlobName": "upload-2025-01-10_result.json",
      "error": null
    },
    {
      "operationId": "456e7890-e89b-12d3-a456-426614174111",
      "status": "Running",
      "blobName": "upload-2025-01-11.pdf",
      "containerName": "uploaded-pdfs",
      "createdAt": "2025-01-11T14:00:00Z",
      "startedAt": "2025-01-11T14:00:03Z",
      "completedAt": null,
      "processedDocuments": 5,
      "totalDocuments": 8,
      "resultBlobName": null,
      "error": null
    }
  ],
  "count": 2
}
```

**Example:**
```bash
# List all operations
curl "https://<function-app>.azurewebsites.net/api/operations?code=<function-key>"

# List only running operations
curl "https://<function-app>.azurewebsites.net/api/operations?status=Running&code=<function-key>"

# List first 10 failed operations
curl "https://<function-app>.azurewebsites.net/api/operations?status=Failed&maxItems=10&code=<function-key>"
```

---

## Operation Status Flow

```
NotStarted ──┐
             │
             ▼
          Running ──┬──> Succeeded
                    │
                    ├──> Failed
                    │
                    └──> Cancelled
```

**Status Descriptions:**
- **NotStarted**: Operation created but not yet picked up by processor
- **Running**: Operation is currently being processed
- **Succeeded**: Operation completed successfully
- **Failed**: Operation failed due to an error
- **Cancelled**: Operation was cancelled by user request

---

## Client Integration Examples

### JavaScript/TypeScript

```typescript
async function processDocument(blobName: string, containerName: string): Promise<void> {
  // Start operation
  const startResponse = await fetch('https://<function-app>.azurewebsites.net/api/operations?code=<key>', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ blobName, containerName })
  });
  
  const { operationId, statusQueryGetUri } = await startResponse.json();
  console.log(`Operation started: ${operationId}`);
  
  // Poll for status
  while (true) {
    const statusResponse = await fetch(`${statusQueryGetUri}&code=<key>`);
    const operation = await statusResponse.json();
    
    console.log(`Status: ${operation.status} (${operation.processedDocuments}/${operation.totalDocuments})`);
    
    if (operation.status === 'Succeeded') {
      console.log('Processing completed!', operation.resultBlobName);
      break;
    } else if (operation.status === 'Failed') {
      console.error('Processing failed:', operation.error);
      break;
    } else if (operation.status === 'Cancelled') {
      console.log('Processing was cancelled');
      break;
    }
    
    // Wait before next poll (respect Retry-After header)
    await new Promise(resolve => setTimeout(resolve, 10000));
  }
}
```

### C#

```csharp
public async Task ProcessDocumentAsync(string blobName, string containerName)
{
    using var httpClient = new HttpClient();
    
    // Start operation
    var startRequest = new { blobName, containerName };
    var startResponse = await httpClient.PostAsJsonAsync(
        "https://<function-app>.azurewebsites.net/api/operations?code=<key>", 
        startRequest);
    
    var startResult = await startResponse.Content.ReadFromJsonAsync<StartOperationResponse>();
    Console.WriteLine($"Operation started: {startResult.OperationId}");
    
    // Poll for status
    while (true)
    {
        var statusResponse = await httpClient.GetAsync(
            $"{startResult.StatusQueryGetUri}&code=<key>");
        var operation = await statusResponse.Content.ReadFromJsonAsync<OperationStatus>();
        
        Console.WriteLine($"Status: {operation.Status} ({operation.ProcessedDocuments}/{operation.TotalDocuments})");
        
        if (operation.Status == "Succeeded")
        {
            Console.WriteLine($"Processing completed! Result: {operation.ResultBlobName}");
            break;
        }
        else if (operation.Status == "Failed")
        {
            Console.WriteLine($"Processing failed: {operation.Error}");
            break;
        }
        else if (operation.Status == "Cancelled")
        {
            Console.WriteLine("Processing was cancelled");
            break;
        }
        
        await Task.Delay(10000); // Wait 10 seconds before next poll
    }
}
```

### Python

```python
import time
import requests

def process_document(blob_name: str, container_name: str):
    base_url = "https://<function-app>.azurewebsites.net/api/operations"
    function_key = "<key>"
    
    # Start operation
    start_response = requests.post(
        f"{base_url}?code={function_key}",
        json={"blobName": blob_name, "containerName": container_name}
    )
    start_data = start_response.json()
    operation_id = start_data["operationId"]
    status_uri = start_data["statusQueryGetUri"]
    
    print(f"Operation started: {operation_id}")
    
    # Poll for status
    while True:
        status_response = requests.get(f"{status_uri}&code={function_key}")
        operation = status_response.json()
        
        print(f"Status: {operation['status']} ({operation['processedDocuments']}/{operation['totalDocuments']})")
        
        if operation["status"] == "Succeeded":
            print(f"Processing completed! Result: {operation['resultBlobName']}")
            break
        elif operation["status"] == "Failed":
            print(f"Processing failed: {operation['error']}")
            break
        elif operation["status"] == "Cancelled":
            print("Processing was cancelled")
            break
        
        time.sleep(10)  # Wait 10 seconds before next poll
```

---

## Cosmos DB Setup

The Operations API requires a Cosmos DB container to store operation state.

**Container Configuration:**
- **Database**: `DocumentOcrDb` (or configured value)
- **Container**: `Operations` (or configured value)
- **Partition Key**: `/id`
- **Throughput**: 400 RU/s minimum (shared with database is recommended)

**Creation via Azure CLI:**
```bash
# Create container with partition key /id
az cosmosdb sql container create \
  --account-name <cosmos-account> \
  --database-name DocumentOcrDb \
  --name Operations \
  --partition-key-path "/id" \
  --throughput 400
```

**Creation via Azure Portal:**
1. Navigate to your Cosmos DB account
2. Open Data Explorer
3. Create new container:
   - Database id: `DocumentOcrDb` (use existing)
   - Container id: `Operations`
   - Partition key: `/id`
   - Throughput: 400 RU/s (or share database throughput)

---

## Error Handling

### Common Error Scenarios

**404 Not Found**
- Operation ID does not exist
- Check the operation ID in the request

**400 Bad Request**
- Invalid request body
- Cannot perform action (e.g., cancel completed operation)
- Missing required fields

**500 Internal Server Error**
- Cosmos DB connection issues
- Storage account connection issues
- Document Intelligence service issues

### Retry Strategy

For transient failures (500 errors), implement exponential backoff:

```typescript
async function retryWithBackoff(fn: () => Promise<any>, maxRetries = 3): Promise<any> {
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await fn();
    } catch (error) {
      if (i === maxRetries - 1) throw error;
      
      const delay = Math.pow(2, i) * 1000; // 1s, 2s, 4s
      await new Promise(resolve => setTimeout(resolve, delay));
    }
  }
}
```

---

## Security

### Function-Level Authentication

All API endpoints use Azure Functions' built-in authentication:
- Function key required in query string: `?code=<function-key>`
- Or in header: `x-functions-key: <function-key>`

### Managed Identity (Recommended for Production)

Configure Managed Identity for:
- Cosmos DB access (RBAC roles)
- Storage account access
- Document Intelligence access

See [DEPLOYMENT.md](DEPLOYMENT.md) for Managed Identity setup instructions.

---

## Monitoring

### Application Insights

The Operations API automatically logs to Application Insights:
- Operation started events
- Status updates
- Errors and exceptions
- Performance metrics

**Example Queries:**

```kusto
// List all operations started in last 24 hours
traces
| where timestamp > ago(24h)
| where message contains "Creating operation"
| project timestamp, operationId=extractjson("$.OperationId", message), blobName=extractjson("$.BlobName", message)

// Operations by status
customMetrics
| where name == "OperationStatus"
| summarize count() by tostring(customDimensions.Status)

// Average processing time
customMetrics
| where name == "OperationDuration"
| summarize avg(value) by bin(timestamp, 1h)
```

---

## Best Practices

1. **Polling Interval**: Respect the `Retry-After` header (default: 10 seconds)
2. **Timeout**: Set client timeout to at least 30 seconds for API calls
3. **Error Handling**: Implement retry logic for transient failures
4. **Status Tracking**: Store operation IDs for later retrieval
5. **Cleanup**: Archive or delete old operations periodically
6. **Cancellation**: Cancel operations when user navigates away
7. **Progress UI**: Show progress bar using `processedDocuments/totalDocuments`

---

## Backwards Compatibility

The queue processor supports both old and new message formats:

**New Format (with operation tracking):**
```json
{
  "OperationId": "123e4567-e89b-12d3-a456-426614174000",
  "Message": {
    "BlobName": "sample.pdf",
    "ContainerName": "uploaded-pdfs",
    "IdentifierFieldName": "identifier"
  }
}
```

**Old Format (without operation tracking):**
```json
{
  "BlobName": "sample.pdf",
  "ContainerName": "uploaded-pdfs",
  "IdentifierFieldName": "identifier"
}
```

This ensures existing integrations continue to work while enabling new operation tracking features.
