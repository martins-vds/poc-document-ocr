# Operations Monitoring UI

The web application includes a dedicated Operations page for monitoring and managing document processing operations. This provides real-time visibility into the extraction operations lifecycle.

## Visual Overview

```
┌────────────────────────────────────────────────────────────────┐
│ Document OCR Review                                      Admin │
│ [Home] [Documents] [Operations*]                       [Logout]│
└────────────────────────────────────────────────────────────────┘

Extraction Operations
Monitor and manage document processing operations

┌─────────────────────┐  ┌──────────────────────────────────────┐
│ Filter by Status ▼  │  │ [Refresh]  ☑ Auto-refresh (every 10s)│
│ All Statuses        │  └──────────────────────────────────────┘
└─────────────────────┘

┌──────────────────────────────────────────────────────────────────────────────────┐
│ Operation ID │ Status    │ File              │ Progress  │ Created  │ Duration   │
├──────────────┼───────────┼───────────────────┼───────────┼──────────┼────────────┤
│ a1b2c3d4     │ Running   │ multi-doc.pdf     │ ████░░ 6/10│ 3:45 PM │ 2m 15s    │
│              │           │ uploaded-pdfs     │           │          │ [Cancel]   │
├──────────────┼───────────┼───────────────────┼───────────┼──────────┼────────────┤
│ e5f6g7h8     │ Succeeded │ invoices.pdf      │ ██████ 15/15│ 2:30 PM │ 5m 42s    │
│              │           │ uploaded-pdfs     │           │          │ [Retry]    │
├──────────────┼───────────┼───────────────────┼───────────┼──────────┼────────────┤
│ i9j0k1l2     │ Failed    │ corrupt.pdf       │ ⚠ Error   │ 1:15 PM │ 0m 8s     │
│              │           │ uploaded-pdfs     │           │          │ [Retry]    │
├──────────────┼───────────┼───────────────────┼───────────┼──────────┼────────────┤
│ m3n4o5p6     │ Cancelled │ large-batch.pdf   │ ███░░░ 3/10│ 12:00 PM│ 1m 30s    │
│              │           │ uploaded-pdfs     │           │          │ [Retry]    │
└──────────────┴───────────┴───────────────────┴───────────┴──────────┴────────────┘
```

## Overview

The Operations UI provides:
- **List View**: View all operations with their current status
- **Status Filtering**: Filter operations by status (NotStarted, Running, Succeeded, Failed, Cancelled)
- **Real-time Progress**: See progress bars for running operations
- **Auto-refresh**: Optional automatic refresh every 10 seconds
- **Operation Management**: Cancel running operations or retry failed/cancelled/completed ones

## Accessing the Operations Page

1. Navigate to the web application
2. Sign in using your Microsoft Entra ID credentials
3. Click **Operations** in the navigation menu

## Features

### Operations List

The operations list displays:

| Column | Description |
|--------|-------------|
| **Operation ID** | Truncated unique identifier for the operation |
| **Status** | Current status with color-coded badge (NotStarted, Running, Succeeded, Failed, Cancelled) |
| **File** | Blob name and container of the PDF being processed |
| **Progress** | Progress bar showing processed documents / total documents |
| **Created** | Timestamp when the operation was created |
| **Duration** | Time elapsed since operation started |
| **Actions** | Cancel or Retry buttons depending on status |

### Status Badges

- **Not Started** (Gray): Operation created but not yet picked up by processor
- **Running** (Blue): Operation is currently being processed with animated progress bar
- **Succeeded** (Green): Operation completed successfully
- **Failed** (Red): Operation failed due to an error
- **Cancelled** (Yellow): Operation was cancelled by user request

### Filtering

Use the **Filter by Status** dropdown to show only operations in a specific state:
- Select a status from the dropdown
- The list automatically refreshes to show matching operations
- Select "All Statuses" to see all operations

### Auto-refresh

Enable the **Auto-refresh** toggle to automatically update the list every 10 seconds:
- Useful for monitoring running operations
- Toggle on/off as needed
- Manually refresh anytime using the **Refresh** button

### Operation Actions

#### Cancel Operation

Available for operations with status `Running` or `NotStarted`:
1. Click the cancel button (⊗ icon) in the Actions column
2. Operation status will update to `Cancelled`
3. For running operations, cancellation happens at the next checkpoint

**Note**: Cannot cancel operations that are already `Succeeded`, `Failed`, or `Cancelled`.

#### Retry Operation

Available for operations with status `Failed`, `Cancelled`, or `Succeeded`:
1. Click the retry button (↻ icon) in the Actions column
2. A new operation is created with the same parameters
3. Success message shows the new operation ID
4. The new operation appears in the list

**Use Cases**:
- Retry failed operations after fixing issues
- Reprocess a document with updated settings
- Resubmit cancelled operations

### Progress Tracking

For running operations, the progress bar shows:
- **Animated striped bar**: Indicates active processing
- **Fraction**: "X / Y" where X is processed documents and Y is total
- **Percentage**: Visual representation of completion

For completed operations:
- **Green bar**: Shows final progress
- **Static display**: No animation

## Configuration

The Operations UI connects to the Operations API (Azure Function). Configure the connection in `appsettings.json`:

```json
{
  "OperationsApi": {
    "BaseUrl": "https://your-function-app.azurewebsites.net",
    "FunctionKey": "your-function-key-or-empty-for-managed-identity"
  }
}
```

For local development (`appsettings.Development.json`):

```json
{
  "OperationsApi": {
    "BaseUrl": "http://localhost:7071",
    "FunctionKey": ""
  }
}
```

**Note**: If using managed identity, leave `FunctionKey` empty. For function-level authentication, provide the key.

## Error Handling

The UI displays error messages for:
- **Failed to load operations**: API connection issues or service errors
- **Failed to cancel operation**: Cannot cancel completed operations or API errors
- **Failed to retry operation**: Cannot retry running operations or API errors

Error messages appear in red alert boxes at the top of the page and can be dismissed.

Success messages appear in green alert boxes after successful operations.

## Technical Details

### API Integration

The Operations UI uses the `IOperationsApiService` to communicate with the Operations API:
- **GET /api/operations**: List all operations with optional filtering
- **GET /api/operations/{id}**: Get specific operation details
- **POST /api/operations/{id}/cancel**: Cancel a running operation
- **POST /api/operations/{id}/retry**: Retry a failed/cancelled/completed operation

See [Operations API Documentation](OPERATIONS-API.md) for detailed API specifications.

### Architecture

```
┌─────────────────────┐
│  Operations.razor   │
│  (Blazor Component) │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────┐
│ OperationsApiService │
│   (HTTP Client)      │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│   Operations API     │
│  (Azure Function)    │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│    Cosmos DB         │
│  (Operations Store)  │
└──────────────────────┘
```

## Best Practices

1. **Monitor Running Operations**: Enable auto-refresh when operations are running
2. **Cleanup Old Operations**: Periodically archive or delete old completed operations
3. **Review Failed Operations**: Check error messages and retry after fixing issues
4. **Use Status Filters**: Filter by status to focus on specific operation types
5. **Track New Operations**: Note new operation IDs when retrying to avoid confusion

## Troubleshooting

### Operations Not Loading

**Symptoms**: Empty list or loading spinner never completes

**Solutions**:
- Check `OperationsApi:BaseUrl` configuration
- Verify Function App is running
- Check network connectivity
- Review browser console for errors
- Verify authentication and authorization

### Cannot Cancel Operation

**Symptoms**: Cancel button disabled or error message

**Solutions**:
- Verify operation is in `Running` or `NotStarted` status
- Check if operation already completed
- Ensure API has permissions to update Cosmos DB

### Auto-refresh Not Working

**Symptoms**: List doesn't update automatically

**Solutions**:
- Verify auto-refresh toggle is enabled
- Check browser console for JavaScript errors
- Try manual refresh button
- Check API connectivity

### Retry Creates Duplicate Operations

**Expected Behavior**: Retry is designed to create a new operation with a new ID. This is intentional to maintain an audit trail of all processing attempts.

## Related Documentation

- [Operations API](OPERATIONS-API.md) - Backend API specification
- [Web Application Usage](WEB-APP-USAGE.md) - General web app guide
- [Architecture](ARCHITECTURE.md) - System architecture overview
