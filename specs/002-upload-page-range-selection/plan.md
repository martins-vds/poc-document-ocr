# Implementation Plan: Upload Page Range Selection

**Branch**: `002-upload-page-range-selection` | **Date**: 2026-05-01 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-upload-page-range-selection/spec.md`

## Summary

Add a per-file, print-dialog–style page-range selector to the WebApp upload flow. The user previews each selected PDF, optionally enters an expression like `3-12, 15` (default = all pages), and submits. The selected ranges flow through the Operations API and queue message into the `PdfProcessorFunction`, which now extracts OCR data only for the requested pages. Page citations within each produced extracted document remain numbered 1..N within that document (per user clarification); the original-PDF page-range expression is persisted on the `Operation` record for audit and shown on the Operations and Review pages.

Approach: introduce a single shared `PageSelection` value object in `DocumentOcr.Common.Models` (parser + normalizer + validator). Wire it through (a) WebApp Blazor `Upload.razor` UI with a PDF preview component, (b) `IOperationsApiService` / `OperationsApiService` request body, (c) Functions `StartOperationRequest.PageRange` → `Operation.PageSelection` → `QueueMessage.PageRange`, and (d) `PdfProcessorFunction` page loop. Backward compatibility is preserved: missing/null page range means "all pages" everywhere.

## Technical Context

**Language/Version**: C# / .NET 10.0 (all four `.csproj` files: three source projects — `Common`, `Processor`, `WebApp` — plus `tests/`).  
**Primary Dependencies**: Azure Functions Worker (isolated, v4), Blazor Server, PDFtoImage + SkiaSharp (page count + rendering, already referenced), Azure.Storage.Queues, Azure.Storage.Blobs, Microsoft.Azure.Cosmos, Newtonsoft.Json (Operation persistence), System.Text.Json (HTTP I/O). For the in-browser PDF preview: Mozilla `pdf.js` served from `wwwroot` (no NuGet dep required).  
**Storage**: Azure Blob Storage (uploaded PDFs in `uploaded-pdfs`, processed in `processed-documents`); Azure Storage Queue `pdf-processing-queue`; Azure Cosmos DB (operations + extracted documents). No new resources.  
**Testing**: xUnit in `tests/` (mirrors `Models/`, `Services/`). Existing fakes/mocks pattern; no live Azure calls.  
**Target Platform**: Linux Azure Functions host (`dotnet-isolated`) + Azure App Service for the Blazor WebApp; modern desktop browsers for the upload UI.  
**Project Type**: Web application (Blazor frontend + Functions HTTP/queue backend) with shared `DocumentOcr.Common` library — **already established**, no new project.  
**Performance Goals**: Preserve existing p95 budgets (Constitution IV): typical 10-page PDF end-to-end < 60 s, Operations API < 500 ms p95. Restricting a 50-page PDF to a 10-page subset SHOULD reduce OCR pages sent by ≥80% (SC-003).  
**Constraints**: Backward-compatible queue contract (FR-014); no new file size limits (50 MB) or batch size limits (10 files); no live Azure calls in tests; keyless auth preserved (no new secrets).  
**Scale/Scope**: ≤10 files per upload; PDFs up to 50 MB / typically <100 pages; new code estimated under ~600 LOC across all projects.

## Constitution Check

*Gate evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.*

| #   | Principle                          | Status | Notes                                                                                                                                                                                                                                                                                                                           |
| --- | ---------------------------------- | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| I   | Code Quality & Maintainability     | PASS   | All new logic exposed via interfaces; `PageSelection` is a small value object in `Common`; PDF preview is a single small Blazor component. No file expected > 300 lines.                                                                                                                                                        |
| II  | Testing Standards (NON-NEGOTIABLE) | PASS   | `PageSelection` parser/validator and pipeline integration get unit tests under `tests/Models/PageSelectionTests.cs` and `tests/Services/PdfProcessorPageRangeTests.cs`. No live Azure calls.                                                                                                                                    |
| III | UX Consistency                     | PASS   | Reuses existing Bootstrap layout/components in `Upload.razor`; adds two predictable controls (preview + range textbox) and a per-row "Apply to all" action. Operations page and Review page surface the range using existing label/badge patterns; updates `docs/REVIEW-PAGE-UX.md` and `docs/WEB-APP-USAGE.md` in the same PR. |
| IV  | Performance & Reliability          | PASS   | Page-count probe runs once per file at upload time (cheap PDFtoImage call already used); extraction loop iterates only the selected indices, strictly reducing work. No new external calls.                                                                                                                                     |
| V   | Security & Secure-by-Default       | PASS   | New input is validated (syntax, bounds, page count) on both client (Blazor) and server (Functions) before any Azure call. No new secrets. Operations API contract change is additive and optional, preserving Entra-protected endpoints unchanged.                                                                              |

**Gate result (initial)**: PASS — no Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/002-upload-page-range-selection/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── operations-api-start.md      # Updated POST /api/operations contract
│   └── queue-message.md             # Updated pdf-processing-queue contract
└── checklists/
    └── requirements.md  # Spec quality checklist (already present)
```

### Source Code (repository root)

```text
src/
├── DocumentOcr.Common/
│   ├── Models/
│   │   └── PageSelection.cs                    # NEW — parser/normalizer/validator value object
│   └── Interfaces/                             # (no new interface; PageSelection is a model)
│
├── DocumentOcr.Processor/
│   ├── Functions/
│   │   ├── OperationsApi.cs                    # MODIFIED — accept optional pageRange in StartOperationRequest
│   │   └── PdfProcessorFunction.cs             # MODIFIED — restrict page loop using QueueMessage.PageSelection
│   ├── Models/
│   │   ├── QueueMessage.cs                     # MODIFIED — add optional PageSelection (string expression)
│   │   └── Operation.cs                        # MODIFIED — add PageSelection (persisted on operation record)
│   └── Services/
│       └── (no changes)
│
└── DocumentOcr.WebApp/
    ├── Components/Pages/
    │   ├── Upload.razor                        # MODIFIED — preview + per-file range field + Apply-to-all
    │   └── (Operations, Review)                # MODIFIED — display selected range / "All pages"
    ├── Components/Shared/
    │   └── PdfRangePicker.razor                # NEW — encapsulates preview + range textbox + validation
    ├── Models/
    │   └── OperationDto.cs                     # MODIFIED — add PageRange string
    ├── Services/
    │   └── OperationsApiService.cs             # MODIFIED — pass pageRange in request body
    └── wwwroot/
        └── lib/pdfjs/                          # NEW — vendored pdf.js for in-browser preview

tests/
├── Models/
│   ├── PageSelectionTests.cs                   # NEW — parser, normalizer, validator
│   └── OperationTests.cs                       # MODIFIED — round-trip PageSelection persistence
└── Services/
    └── PdfProcessorPageRangeTests.cs           # NEW — verifies only selected pages are sent to OCR
```

**Structure Decision**: Reuse the existing three-project layout mandated by the constitution (`Common` / `Processor` / `WebApp`). The shared `PageSelection` value object lives in `DocumentOcr.Common.Models` so the WebApp (validation), the Functions HTTP entry point (validation + persistence), and the Functions queue worker (consumption) all share one canonical implementation — eliminating any chance of UI-only validation drifting from server-side enforcement.

## Phase 0 — Research

See [research.md](research.md). Decisions:

1. **Range syntax**: print-dialog only (`N`, `N-M`, comma-separated, whitespace-tolerant); no wildcards (`*`, `all`, open-ended `5-`). Confirmed by user.
2. **Citation numbering inside extracted documents**: 1..N within each produced document (the OCR-extracted page index for that document), **not** the original PDF page number. Confirmed by user. The original-PDF mapping is held on the `Operation` record only.
3. **In-browser PDF preview**: Vendor Mozilla pdf.js (Apache-2.0) under `wwwroot/lib/pdfjs/`. Rationale: client-side only, zero server load, works offline, no NuGet dependency added to the Blazor project, well-supported in modern browsers. Alternatives rejected: server-rendered thumbnails (extra Functions call + bandwidth + latency before user has even submitted); `<embed type="application/pdf">` (no JS API for page count, inconsistent across browsers).
4. **Page-count probe at upload time**: WebApp performs the probe in JavaScript (pdf.js exposes `numPages`) so we can validate the range before the upload begins. The Functions HTTP entry point re-validates against the supplied expression syntactically and against `>=1` bound; final per-page validation happens in the worker after the PDF is downloaded (any page > actual count is a hard error that fails the operation, since the source of truth is the stored PDF).
5. **Backward compatibility**: `PageSelection` field is optional/nullable everywhere. Missing or empty = "all pages". Existing queue messages and existing operations remain valid; existing tests untouched.
6. **Multi-file UX**: Per-file range textbox plus a single "Apply to all" button that copies the first file's expression to every other file. Reuses existing `selectedFiles` list shape in `Upload.razor`.

## Phase 1 — Design

### Data model

See [data-model.md](data-model.md). New/changed entities:

- **`PageSelection` (new value object)** — fields: `Expression` (string, original input, e.g. `"3-12, 15"`), `Pages` (`IReadOnlyList<int>`, normalized & deduplicated 1-indexed). Static `TryParse(string?, int? maxPage, out PageSelection, out string error)` + `IsAllPages` (true when null/empty input).
- **`Operation` (modified)** — adds `PageSelection? PageSelection { get; set; }` (persisted to Cosmos). Null = "All pages".
- **`QueueMessage` (modified)** — adds `string? PageRange { get; set; }` (carries the original expression; the worker re-parses against the actual page count after download).
- **`StartOperationRequest` (Functions HTTP)** — adds optional `pageRange` JSON property.
- **`OperationDto` (WebApp)** — adds `PageRange` string.

### Contracts

See [contracts/](contracts/):

- `operations-api-start.md`: `POST /api/operations` — request now optionally includes `pageRange`. Response unchanged in shape; the persisted `Operation` resource (returned by `GET /api/operations/{id}`) gains a `pageRange` field.
- `queue-message.md`: `pdf-processing-queue` — `Message.PageRange` (string, optional, nullable). Old messages without the field are processed as "all pages".

### Quickstart

See [quickstart.md](quickstart.md): build, run Azurite + Functions + WebApp, exercise the new range field for both default and restricted cases, and verify the persisted operation reflects the chosen range.

### Agent context update

Updated the `<!-- SPECKIT START --> ... <!-- SPECKIT END -->` block in `.github/copilot-instructions.md` to point to this plan.

## Phase 1 — Re-evaluated Constitution Check

| #   | Principle                      | Status | Notes                                                                                                                                                                                                         |
| --- | ------------------------------ | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| I   | Code Quality & Maintainability | PASS   | Design keeps all changes additive: one new file in `Common`, one new Blazor component, one new vendored static asset directory; all touched services already exist.                                           |
| II  | Testing Standards              | PASS   | Two new test files cover (a) the parser/normalizer (pure logic, deterministic) and (b) pipeline restriction (mocked `IDocumentIntelligenceService`). Existing tests unchanged → no regression in green count. |
| III | UX Consistency                 | PASS   | New `PdfRangePicker` follows existing list-group + form-control patterns in `Upload.razor`; range display on Operations/Review pages uses existing badge/label vocabulary; UX docs updated.                   |
| IV  | Performance & Reliability      | PASS   | Worker change is a strict subset of work; no new external calls; preview is fully client-side. Operations API change adds a small string field — unmeasurable latency impact.                                 |
| V   | Security & Secure-by-Default   | PASS   | All ingress points validate `PageSelection` before use; missing field defaults safely to "all pages" (current behavior); no new secrets, dependencies, or endpoints.                                          |

**Gate result (post-design)**: PASS.

## Complexity Tracking

*No constitutional violations to justify.*
