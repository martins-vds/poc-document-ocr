# Contract: `POST /api/operations` (Start Operation)

**Feature**: 002-upload-page-range-selection  
**Endpoint**: `POST {OperationsApi:BaseUrl}/api/operations`  
**Auth**: Function key (`?code=…`) at the API edge; the WebApp caller is itself behind Entra ID.  
**Function**: `DocumentOcr.Processor.Functions.OperationsApi.StartOperation`

This contract describes the **delta** introduced by feature 002. Existing fields and behaviors are preserved verbatim.

---

## Request

`Content-Type: application/json`

```json
{
  "blobName": "20260501-120000-invoice-batch.pdf",
  "containerName": "uploaded-pdfs",
  "pageRange": "3-12, 15"
}
```

| Field           | Type   | Required     | Description                                                                                                          |
| --------------- | ------ | ------------ | -------------------------------------------------------------------------------------------------------------------- |
| `blobName`      | string | yes          | Existing.                                                                                                            |
| `containerName` | string | yes          | Existing.                                                                                                            |
| `pageRange`     | string | **no** (NEW) | Print-dialog–style expression. Omit, send `null`, or send `""` to mean "all pages" (preserves pre-feature behavior). |

### `pageRange` grammar

```
expr   := token ("," token)*
token  := page | page "-" page
page   := positive integer (≥ 1)
```

Whitespace around any token or hyphen is tolerated. Pages are 1-indexed. Tokens may overlap; duplicates are silently merged.

### Validation performed by the endpoint

1. `blobName` and `containerName` non-empty (existing behavior — `400 Bad Request` with text body if missing).
2. **NEW**: If `pageRange` is non-empty, parse it via `PageSelection.TryParse(pageRange, maxPage: null, ...)`:
   - On syntactic error or page < 1 → `400 Bad Request`, body = the parser's error message string.
3. The endpoint does **not** verify the upper bound (`page ≤ totalPages`) — the actual page count is unknown until the worker downloads the PDF. Out-of-bounds errors surface as a failed operation.

---

## Responses

### `202 Accepted` (success — unchanged shape)

`Headers: Location: /api/operations/{operationId}`

```json
{
  "operationId": "0f3...",
  "status": "NotStarted",
  "statusQueryGetUri": "https://.../api/operations/0f3..."
}
```

The created `Operation` resource (returned by `GET /api/operations/{operationId}`) now includes the `pageRange` field — see below.

### `400 Bad Request` (validation failed — extended)

Body is a plain-text message (matches existing convention). New possible messages from `PageSelection.TryParse`:

- `Invalid token '<tok>': use page numbers or N-M ranges.`
- `Page numbers must be 1 or greater.`
- `Range '<N>-<M>' has start greater than end.`

### `500 Internal Server Error` (unchanged)

---

## Side-effects (delta)

- The created `Operation` document in Cosmos DB stores `pageSelection: { expression, pages }` when `pageRange` was supplied, or `pageSelection: null` when omitted.
- The queued `QueueMessage` carries `pageRange` (string, optional) inside the `Message` field of the existing `QueueMessageWrapper` envelope.

---

## `GET /api/operations/{operationId}` response — extension

The response object adds one field (all other fields unchanged):

```json
{
  "operationId": "0f3...",
  "status": "Succeeded",
  "blobName": "...",
  "containerName": "...",
  "createdAt": "...",
  "startedAt": "...",
  "completedAt": "...",
  "processedDocuments": 5,
  "totalDocuments": 5,
  "resultBlobName": null,
  "error": null,
  "cancelRequested": false,
  "pageRange": "3-12, 15"
}
```

| Field       | Type           | Description                                                                                              |
| ----------- | -------------- | -------------------------------------------------------------------------------------------------------- |
| `pageRange` | string \| null | The original expression supplied at creation. `null` or absent ⇒ "All pages" (display rule for clients). |

`GET /api/operations` (list) returns the same shape per item.

---

## Backward compatibility

- Clients that omit `pageRange` get **identical** behavior to today.
- Existing operation documents in Cosmos that lack `pageSelection` deserialize with the field as `null` and the API returns `pageRange: null` (clients display "All pages").
- The cancel and retry endpoints are not affected by this feature.
