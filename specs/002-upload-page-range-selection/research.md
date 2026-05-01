# Phase 0 Research: Upload Page Range Selection

**Feature**: 002-upload-page-range-selection  
**Date**: 2026-05-01

This document records the research and decisions resolving the unknowns in the plan's Technical Context. All `NEEDS CLARIFICATION` items are resolved.

---

## R1. Page-range syntax

**Decision**: Print-dialog–style only — comma-separated list of 1-indexed `N` or `N-M` tokens, whitespace tolerant. No wildcards, no open-ended ranges, no negative numbers, no "all" / `*` keywords.

**Rationale**: Matches every desktop OS print dialog the target user already knows. Smallest grammar that satisfies the user's stated motivation ("just like when you print a document"). User explicitly confirmed.

**Alternatives considered**:
- Open-ended `5-` ("from 5 to end"). Rejected: requires "actual page count" knowledge in two places (UI must materialize, server must re-resolve), doubling the validation paths for marginal value.
- Wildcard `all` / blank-means-all. Partially adopted — blank input still means "all" but it is implicit (default), not a parsed token.
- Page lists with negative offsets (`-3` = "skip last 3"). Rejected: not a print-dialog convention; out of scope.

**Reference grammar** (informal):

```
expr      := token ("," token)*
token     := page | page "-" page
page      := [1-9][0-9]*
whitespace tolerated around any token; "-" may have surrounding spaces
```

Validation rules (in order):
1. Empty/whitespace input → represents "all pages" (no error in the parser; UI flags as default).
2. Each token must match `page` or `page-page`.
3. Each `page` must be ≥ 1 (and ≤ `maxPage` when `maxPage` is supplied).
4. In `N-M`, require `N ≤ M`.
5. Final list deduplicated and sorted ascending.
6. After dedup, the resulting page list must be non-empty (this is unreachable given rules 1–4, but asserted defensively).

---

## R2. Citation numbering inside extracted documents

**Decision**: Page citations and per-page provenance produced by OCR for each extracted document remain numbered **1..N within that document** (the OCR-extracted page index for that document). They do **not** reference the original uploaded PDF's page numbers. The original-PDF mapping (the user's typed expression) is stored once at the **operation** level.

**Rationale**: User-confirmed semantics. Keeps the existing aggregation/output behavior intact (extracted documents are standalone artifacts that can be reviewed without context of the uploaded source) and avoids touching `PageProvenanceEntry` / `DocumentResult` schemas. Reviewers who need to trace a document back to a specific subset of the source PDF can read the page-range string from the operation.

**Alternatives considered**:
- Renumber to original PDF pages (5..10 instead of 1..6). Rejected by user.
- Dual numbering (both). Rejected: doubles persisted state for no measured reviewer demand and would force schema changes in `PageProvenanceEntry`.

**Implication**: No change required to `DocumentResult`, `PageProvenanceEntry`, or any aggregation logic. The processor's existing `pageNumber = i + 1` loop variable already gives the per-document index after we restrict the input list — it just iterates over fewer pages.

---

## R3. In-browser PDF preview library

**Decision**: Vendor Mozilla **pdf.js** under `src/DocumentOcr.WebApp/wwwroot/lib/pdfjs/` and load it via a small Blazor JS interop wrapper inside the new `PdfRangePicker.razor` component. Use the prebuilt `pdf.mjs` + `pdf.worker.mjs` distribution.

**Rationale**:
- 100% client-side preview → zero load on the Functions host or WebApp server, zero extra Azure cost, no bandwidth round-trip before the user has even submitted.
- Provides a JS API that exposes the document's `numPages` deterministically — required for **client-side** range validation per FR-004.
- Apache-2.0 license, actively maintained by Mozilla, ships in Firefox; safe to vendor.
- No new NuGet/server dependency added to `DocumentOcr.WebApp.csproj`; nothing to update in `Common`.

**Alternatives considered**:
- `<embed type="application/pdf">` / `<iframe src=*.pdf>` (browser native viewer). Rejected: no JS surface for `numPages`, inconsistent across Edge/Chrome/Firefox/Safari, no programmatic page-by-page control for preview.
- Server-rendered thumbnails via the existing `PdfToImageService`. Rejected: forces the WebApp to call the Functions host (or duplicate the rendering pipeline) before the user has submitted; adds latency and operational complexity to a pure UX concern; uploads of large PDFs would feel slow before any work is committed.
- Third-party JS PDF viewers (`PDFTron`, `PSPDFKit`). Rejected: commercial / heavy / out of POC scope.

**Operational note**: pdf.js is loaded only on the Upload page (lazy via JS interop) and is gated behind the Entra-protected `[Authorize]` attribute, so it is not exposed to anonymous traffic.

---

## R4. Where to validate the range expression

**Decision**: Validate at three layers, defense-in-depth:

1. **Browser (Blazor + pdf.js)**: parses the expression via `PageSelection.TryParse` (shared with the server) — we call into the same C# code from the Blazor component, no JS port needed. Resolves `numPages` from pdf.js to bound-check. Disables the upload button on any error.
2. **Operations API HTTP entry (`StartOperation`)**: parses again with `maxPage = null` (HTTP entry has not downloaded the PDF). Rejects malformed expressions and pages `< 1`. Persists the **original expression** on the `Operation`.
3. **Worker (`PdfProcessorFunction`)**: after downloading the PDF and obtaining the actual page count, parses again with `maxPage = actualPageCount`. Any out-of-bounds page fails the operation with a clear error (because at that point the source of truth — the stored PDF — has been seen).

**Rationale**: Layer 1 is for UX; layer 2 is for any non-UI caller of the API (and is required by Constitution V — "Authorization checks MUST be performed server-side"); layer 3 catches mismatch between the page count the browser saw and the page count of what was actually uploaded (e.g., race or tampering). Reusing one parser eliminates drift.

**Alternatives considered**:
- Validate only on the worker. Rejected: wastes upload + queue + dequeue cycles on bad input; violates FR-004 and SC-004.
- Validate only on the browser. Rejected: violates Constitution V; would let direct API callers smuggle malformed input.

---

## R5. Operation persistence shape

**Decision**: Persist `PageSelection` on `Operation` as a complex object: `{ "expression": "3-12, 15", "pages": [3,4,5,6,7,8,9,10,11,12,15] }`. Keep it nullable. Cosmos serialization continues to use Newtonsoft.Json (consistent with existing `Operation` attributes).

**Rationale**: Storing both keeps the human-readable form for the UI ("3-12, 15") and the machine-resolved form for any future re-run / audit query, without re-parsing on read. Nullable preserves backward compatibility with existing operation documents (FR-014).

**Alternatives considered**:
- Store the expression only. Rejected: every reader would have to re-parse, and the resolved list could change meaning if the parser ever evolved.
- Store the resolved list only. Rejected: loses the user's original intent (the spec requires showing the originally-selected range on the Operations page — FR-012).

---

## R6. Multi-file UX ("Apply to all")

**Decision**: Render one `PdfRangePicker` per selected file inside the existing `selectedFiles` `<ul class="list-group">`. Render a single small "Apply to all" button beside the first file only. Clicking it copies the first file's expression to every other file's picker (and re-runs each picker's validation against its own page count). No bulk-edit dialog.

**Rationale**: Lowest-friction implementation that satisfies FR-010 and matches the user's wording. Existing list-group pattern in `Upload.razor` is preserved (Constitution III).

**Alternatives considered**:
- Single shared range across all files. Rejected by user and by FR-010.
- Bulk-edit modal. Rejected: heavier UX for a feature whose default is "all pages" (most users won't touch it).

---

## R7. Preserving the queue contract

**Decision**: Add `PageRange` (string, optional/nullable) to `QueueMessage`. Old messages without the field deserialize with `PageRange = null`, which the worker treats as "all pages". The `QueueMessageWrapper` shape is unchanged (still `{ OperationId, Message }`).

**Rationale**: System.Text.Json default behavior tolerates missing properties → zero-impact backward compatibility (FR-014). No queue migration or version field needed.

**Alternatives considered**:
- Add a new queue with a v2 contract. Rejected: massively over-engineered for a single optional string.
- Send only the operation ID and read the range from Cosmos in the worker. Rejected: adds a Cosmos read on the hot path for every message and couples the worker more tightly to the operation store than it currently is.

---

## R8. Performance impact

**Decision**: No measurable regression expected.

- Upload UI: pdf.js renders one page lazily; the bottleneck remains user network upload speed.
- Operations API: one extra `PageSelection.TryParse` call (microseconds) per request.
- Worker: strictly less work than today when a non-default range is supplied (skips OCR for excluded pages). With default "all pages", behavior is identical.

The 60-second p95 budget for a 10-page PDF (Constitution IV) is unaffected; SC-003's 80% reduction claim holds when the user excludes 80% of pages because OCR per page dominates the pipeline.

---

## Summary

All open items in the plan's Technical Context are resolved. No `NEEDS CLARIFICATION` markers remain. Ready for Phase 1 design.
