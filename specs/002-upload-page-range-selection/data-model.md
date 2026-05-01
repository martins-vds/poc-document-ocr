# Phase 1 Data Model: Upload Page Range Selection

**Feature**: 002-upload-page-range-selection  
**Date**: 2026-05-01

This document defines the new and modified entities. Property names use C# / .NET conventions; JSON property names follow the existing solution conventions (camelCase via `JsonPropertyName` for HTTP boundaries; Newtonsoft `JsonProperty` for Cosmos persistence on `Operation`).

---

## 1. `PageSelection` (NEW value object)

**Location**: `src/DocumentOcr.Common/Models/PageSelection.cs`  
**Purpose**: Single canonical representation of "which pages of a PDF to process." Shared by WebApp validation, Operations API entry, and the queue worker.

### Fields

| Property     | Type                 | Description                                                                                                                                   |
| ------------ | -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| `Expression` | `string`             | Original user-typed (or system-generated) expression, normalized for whitespace, e.g. `"3-12, 15"`. Empty string when "all pages" is implied. |
| `Pages`      | `IReadOnlyList<int>` | Sorted, deduplicated, 1-indexed page numbers. Empty when "all pages".                                                                         |
| `IsAllPages` | `bool`               | `true` when `Expression` is null/empty (i.e., implicit default).                                                                              |

### Behavior

```csharp
public sealed class PageSelection
{
    public static PageSelection All { get; } = new(string.Empty, Array.Empty<int>());

    public string Expression { get; }
    public IReadOnlyList<int> Pages { get; }
    public bool IsAllPages => Expression.Length == 0;

    public static bool TryParse(string? input, int? maxPage,
                                out PageSelection result, out string? error);

    // Materializes the absolute page list against an actual page count.
    // For IsAllPages, returns 1..totalPages. For explicit selections,
    // returns Pages, but throws if any page > totalPages.
    public IReadOnlyList<int> Resolve(int totalPages);
}
```

### Validation rules (enforced by `TryParse`)

1. `null` or whitespace input → `result = PageSelection.All`, no error.
2. Each token must match `\d+` or `\d+\s*-\s*\d+`; otherwise error `"Invalid token '<tok>': use page numbers or N-M ranges."`.
3. Each page number must be ≥ 1; otherwise error `"Page numbers must be 1 or greater."`.
4. If `maxPage` is provided, every page must be ≤ `maxPage`; otherwise error `"Page <p> exceeds document length (<maxPage>)."`.
5. In `N-M`, `N ≤ M`; otherwise error `"Range '<N>-<M>' has start greater than end."`.
6. Final list is deduplicated and sorted ascending (overlapping/repeated tokens are silently merged — FR-006).
7. After dedup, the page list must be non-empty (defensive; unreachable given rules 1–4).

> **Note**: Callers MUST check `IsAllPages` to detect the "all pages" case rather than `Pages.Count == 0`. `IsAllPages` is the sole source of truth; an empty `Pages` list is a side-effect of that state.

### Persistence (Cosmos via Newtonsoft on `Operation`)

```json
{
  "expression": "3-12, 15",
  "pages": [3,4,5,6,7,8,9,10,11,12,15]
}
```

### Wire format on `QueueMessage` and HTTP

The queue and HTTP boundaries carry only the **expression string** (`pageRange`). The full `PageSelection` is reconstructed by the receiver via `TryParse`. This keeps the queue contract minimal and avoids round-tripping a redundant `pages` array.

---

## 2. `Operation` (MODIFIED)

**Location**: `src/DocumentOcr.Processor/Models/Operation.cs`

### New field

| Property        | Type             | Cosmos JSON       | Description                                                                                                                                                                                                 |
| --------------- | ---------------- | ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PageSelection` | `PageSelection?` | `"pageSelection"` | `null` ⇒ "All pages" (preserves backward compatibility with existing operation records — FR-014). Set at operation creation by the HTTP `StartOperation` handler from the parsed `pageRange` request field. |

### State transitions

Unchanged. `PageSelection` is set once at creation and never mutated.

### Backward compatibility

- Existing Cosmos documents without `pageSelection` deserialize with `PageSelection = null` → treated as "All pages" everywhere.
- New operations always set the field (either to a parsed `PageSelection` or to an explicit `PageSelection.All` to make the "All pages" decision visible in storage). **Decision**: store `null` for "all pages" rather than the `All` sentinel — keeps storage shape identical for the common case and makes the back-compat read path trivially uniform.

---

## 3. `QueueMessage` (MODIFIED)

**Location**: `src/DocumentOcr.Processor/Models/QueueMessage.cs`

### New field

| Property    | Type      | JSON          | Description                                                                                                                                           |
| ----------- | --------- | ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PageRange` | `string?` | `"pageRange"` | Optional. The original expression string. `null` or empty ⇒ all pages. The worker re-parses this against the actual page count of the downloaded PDF. |

Existing fields (`BlobName`, `ContainerName`, `UseManualDetection`) are unchanged.

### Backward compatibility

- Old messages without `pageRange` deserialize with `PageRange = null` → "all pages".
- Wrapper envelope `{ "OperationId": "...", "Message": { ... } }` is unchanged.

---

## 4. `StartOperationRequest` (MODIFIED)

**Location**: `src/DocumentOcr.Processor/Functions/OperationsApi.cs`

### New field

| Property    | Type      | JSON          | Description                                           |
| ----------- | --------- | ------------- | ----------------------------------------------------- |
| `PageRange` | `string?` | `"pageRange"` | Optional. Same semantics as `QueueMessage.PageRange`. |

The HTTP handler:
1. Reads `pageRange`.
2. Calls `PageSelection.TryParse(pageRange, maxPage: null, out var selection, out var err)`.
3. On parse error → `400 Bad Request` with the `err` string in the body.
4. Otherwise → stores `selection` (or `null` if `IsAllPages`) on the `Operation` and forwards `pageRange` (the raw expression, possibly null) on the `QueueMessage`.

---

## 5. `OperationDto` (MODIFIED, WebApp)

**Location**: `src/DocumentOcr.WebApp/Models/OperationDto.cs`

### New field

| Property    | Type      | Description                                                                                               |
| ----------- | --------- | --------------------------------------------------------------------------------------------------------- |
| `PageRange` | `string?` | Display-only echo of `Operation.PageSelection.Expression`. `null` or empty ⇒ render "All pages" (FR-012). |

The Operations API `GetOperation` response is extended to include `pageRange` (top-level string) so this DTO can populate it directly.

---

## 6. UI-only model: `UploadFileEntry` (NEW, WebApp internal)

**Location**: in `Upload.razor` `@code` block (no separate file required)  
**Purpose**: Replace the current bare `IBrowserFile` list with a per-file aggregate that carries the picker state.

```csharp
private sealed class UploadFileEntry
{
    public IBrowserFile File { get; init; } = default!;
    public int? TotalPages { get; set; }       // resolved by pdf.js after preview
    public string RangeExpression { get; set; } = string.Empty;
    public string? RangeError { get; set; }    // null when valid
    public PageSelection? Selection { get; set; }
}
```

The list `selectedFiles` becomes `List<UploadFileEntry>`. Submission is gated on `selectedFiles.All(e => e.RangeError is null)`.

---

## Entity relationships

```
UploadFileEntry (UI)
   └─ produces ─▶ StartOperationRequest { blobName, containerName, pageRange? }
                       └─ creates ─▶ Operation { ..., PageSelection? }
                                          └─ enqueues ─▶ QueueMessage { ..., PageRange? }
                                                              └─ consumed by ─▶ PdfProcessorFunction
                                                                                      └─ uses PageSelection.Resolve(totalPages)
                                                                                          to limit OCR loop
```

All wire crossings carry only the expression string; `PageSelection.TryParse` reconstructs the typed model on each side.
