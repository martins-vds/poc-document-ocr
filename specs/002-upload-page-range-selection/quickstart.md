# Quickstart: Upload Page Range Selection

**Feature**: 002-upload-page-range-selection  
**Audience**: Developer running the solution locally to validate the feature end-to-end.

## Prerequisites

- .NET 10.0 SDK
- Azure Functions Core Tools v4
- Azurite (`npm i -g azurite`)
- Cosmos DB Emulator (or a real Cosmos endpoint configured in `local.settings.json`)
- Document Intelligence endpoint + key configured in `local.settings.json`
- Modern desktop browser (Chrome/Edge/Firefox) for the WebApp upload page
- A multi-page PDF on disk (≥ 5 pages recommended) — preferably one with a recognizable preamble (cover/TOC) and meaningful content after it

## Build

```bash
# From repo root
dotnet build DocumentOcr.sln
```

Expected: zero errors, zero new warnings (Constitution I).

## Run

In three separate terminals:

```bash
# 1) Local storage emulator (queues + blobs)
azurite --silent --location /tmp/azurite

# 2) Functions host (Operations API + queue worker)
cd src/DocumentOcr.Processor
cp local.settings.json.template local.settings.json   # only if missing; then edit values
func start

# 3) Blazor WebApp
cd src/DocumentOcr.WebApp
cp appsettings.Development.json.template appsettings.Development.json   # only if missing
dotnet run
```

Open the WebApp URL printed by `dotnet run`, sign in, and navigate to **Upload**.

## Validation scenarios

### Scenario A — Default ("All pages") path is unaffected (FR-003, FR-014, SC-005)

1. On the Upload page, select a multi-page PDF.
2. Confirm the new preview pane renders the document and shows the total page count.
3. **Do not** type anything into the page-range field.
4. Click **Upload and Start Extraction**.
5. Open the Operations page; the new operation MUST display `Page range: All pages`.
6. After completion, open the Review page for any extracted document and confirm citations are numbered 1..N within that document (existing behavior).

**Expected**: Identical extraction outcome to before this feature was added.

### Scenario B — Restrict a 20-page PDF to pages 3–12 and 15 (FR-002, FR-005, FR-006, FR-009, US1)

1. Select a 20-page PDF and wait for the preview/page-count to load.
2. In the page-range field, type `3-12, 15`.
3. Confirm the inline summary shows `11 pages selected: 3–12, 15`.
4. Click **Upload and Start Extraction**.
5. Open the operation; verify `Page range: 3-12, 15` is shown alongside the other metadata.
6. Verify (e.g., via Functions logs) that OCR was invoked exactly 11 times — no calls for pages 1, 2, 13, 14, 16–20.

### Scenario C — Inline validation blocks bad input (FR-004, FR-007, US2)

For each of the following inputs, type it into the range field and confirm:

| Input                    | Expected inline error                                                | Submit enabled? |
| ------------------------ | -------------------------------------------------------------------- | --------------- |
| `25-30` (on 20-page PDF) | "Page 30 exceeds document length (20)." (or equivalent)              | No              |
| `abc`                    | "Invalid token …"                                                    | No              |
| `1-`                     | "Invalid token …"                                                    | No              |
| `5-3`                    | "Range '5-3' has start greater than end."                            | No              |
| `0`                      | "Page numbers must be 1 or greater."                                 | No              |
| `   ` (whitespace only)  | (empty is the implicit default — no error; behaves like "all pages") | Yes             |

### Scenario D — Per-file ranges with "Apply to all" (FR-010)

1. Select 3 PDFs of different lengths.
2. Verify each row in the file list has its own preview + range field.
3. Type `2-5` in the **first** file's range field, then click **Apply to all**.
4. Confirm the second and third files now show `2-5` in their fields and re-validate against their own page counts.
5. If any file has fewer than 5 pages, that row MUST display the bound error and Submit MUST be disabled.

### Scenario E — Backward compatibility with existing operations (FR-014)

1. (If you have an operation document in Cosmos from before this feature) Open the Operations page and confirm it displays `Page range: All pages` for that legacy operation.
2. Hit `GET /api/operations/{legacyOperationId}` directly; the `pageRange` field MUST be `null` or absent.

### Scenario F — Citations remain document-local (FR-011, SC-006)

1. Use Scenario B's restricted upload (`3-12, 15`).
2. Open one of the produced extracted documents in Review.
3. Confirm per-page provenance shows pages numbered 1..N **within that document** (e.g., a 3-page extracted document cites pages 1, 2, 3), not 3–5 of the source PDF.

### Scenario G — Corrupt PDF rejected at upload time (FR-015)

1. Try to select a deliberately corrupt or password-protected PDF.
2. Verify the UI rejects the file with a clear error and that **no operation is started** (Operations page count unchanged; nothing in the queue).

## Tests

```bash
dotnet test
```

Expected: all existing tests still pass; the new tests added for this feature pass:

- `tests/Models/PageSelectionTests.cs` (parser, normalizer, validator)
- `tests/Models/OperationTests.cs` (round-trips `PageSelection`)
- `tests/Services/PdfProcessorPageRangeTests.cs` (mocked OCR receives only selected pages)

## Cleanup

Stop `func start`, `dotnet run`, and Azurite. Drop the local Cosmos containers if you want a clean slate.
