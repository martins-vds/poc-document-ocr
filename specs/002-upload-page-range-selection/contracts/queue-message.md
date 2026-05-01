# Contract: `pdf-processing-queue` Message

**Feature**: 002-upload-page-range-selection  
**Queue**: `pdf-processing-queue` (Azure Storage Queue, base64-encoded)  
**Producer**: `OperationsApi.StartOperation`  
**Consumer**: `PdfProcessorFunction.Run` (queue trigger)

This contract describes the **delta** introduced by feature 002. The envelope shape is unchanged.

---

## Envelope (unchanged)

```json
{
  "OperationId": "0f3...",
  "Message": { ...QueueMessage... }
}
```

---

## `Message` payload — extended

```json
{
  "BlobName": "20260501-120000-invoice-batch.pdf",
  "ContainerName": "uploaded-pdfs",
  "UseManualDetection": false,
  "PageRange": "3-12, 15"
}
```

| Field                | Type           | Required     | Description                                                                     |
| -------------------- | -------------- | ------------ | ------------------------------------------------------------------------------- |
| `BlobName`           | string         | yes          | Existing.                                                                       |
| `ContainerName`      | string         | yes          | Existing.                                                                       |
| `UseManualDetection` | bool           | no           | Existing.                                                                       |
| `PageRange`          | string \| null | **no** (NEW) | Original print-dialog–style expression. Absent, `null`, or empty ⇒ "all pages". |

---

## Consumer behavior

After downloading the PDF and obtaining `totalPages` from `PdfToImageService`:

1. Parse: `PageSelection.TryParse(message.PageRange, maxPage: totalPages, out var selection, out var err)`.
2. On parse failure → mark operation `Failed`, set `Error = err`, complete; do **not** call OCR.
3. On success:
   - Compute `selectedPages = selection.IsAllPages ? Enumerable.Range(1, totalPages).ToList() : selection.Pages`.
   - Iterate the existing `imageStreams` list over `selectedPages` only, addressing each page as `imageStreams[selectedPage - 1]` (no re-decode of the PDF). Streams for excluded pages are disposed up front.
   - The per-page `pageNumber` passed to `PageOcrResult` is the **selected page's index** position within the chosen subset (1..N), so existing aggregation, citation, and provenance behavior is preserved (per FR-011: in-document citations remain 1..N).
   - Update `operation.TotalDocuments` based on the post-aggregation count, as today.

---

## Backward compatibility

- Old messages without `PageRange` deserialize with `PageRange = null` → "all pages" → identical to current behavior.
- No queue version field, no migration. New messages remain readable by any consumer that ignores unknown fields (which `System.Text.Json` does by default).
