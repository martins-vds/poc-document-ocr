# Tasks: Upload Page Range Selection

**Input**: Design documents from `/specs/002-upload-page-range-selection/`  
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)  
**Branch**: `002-upload-page-range-selection`

**Tests**: Included. Constitution II ("Testing Standards") is NON-NEGOTIABLE for this repository — every behavioral change ships with tests under `tests/`.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested, and demoed independently. Foundational phase contains the shared `PageSelection` model and contract field additions that every story depends on.

## Format

`- [ ] T### [P?] [Story?] Description with file path`

- `[P]` — parallelizable with other `[P]` tasks (different files, no dependency on incomplete tasks)
- `[US#]` — required for user-story phase tasks
- File paths are workspace-relative

## Path conventions

This is a multi-project .NET solution per the constitution:
- Shared models: `src/DocumentOcr.Common/`
- Functions host (HTTP + queue worker): `src/DocumentOcr.Processor/`
- Blazor WebApp: `src/DocumentOcr.WebApp/`
- Tests: `tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: One-time additions to the workspace that are not specific to any story.

- [ ] T001 [P] Vendor Mozilla pdf.js (Apache-2.0) prebuilt distribution into `src/DocumentOcr.WebApp/wwwroot/lib/pdfjs/` (files: `pdf.mjs`, `pdf.worker.mjs`, plus `LICENSE`); ensure files are included by the SPA static-files pipeline (no `.csproj` change required, but verify with `dotnet build src/DocumentOcr.WebApp`).
- [ ] T002 Add a "Third-party notices" line for pdf.js to `src/DocumentOcr.WebApp/wwwroot/lib/pdfjs/README.md` (new file) noting version, source URL, and Apache-2.0 license.

**Checkpoint**: pdf.js is reachable at `/lib/pdfjs/pdf.mjs` from the running WebApp.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared `PageSelection` value object and the additive contract fields. Every user story consumes these. **No US task may begin until this phase is complete.**

- [ ] T003 [P] Create `PageSelection` value object in `src/DocumentOcr.Common/Models/PageSelection.cs` per [data-model.md §1](data-model.md), implementing: `Expression`, `Pages`, `IsAllPages`, `static All`, `static bool TryParse(string?, int?, out PageSelection, out string?)`, and `IReadOnlyList<int> Resolve(int totalPages)`. Validation rules per [research.md R1](research.md) and [data-model.md §1](data-model.md).
- [ ] T004 [P] Create `tests/Models/PageSelectionTests.cs` covering, at minimum: null/empty → `IsAllPages` true; whitespace-tolerant parsing of `3 - 12 ,  15`; deduplication of overlapping tokens (`3-7, 5-10` → 8 unique pages 3..10); reversed bounds rejected; `0` rejected; `abc` rejected; `1-` rejected; `maxPage` upper-bound enforcement; `Resolve(totalPages)` returns 1..N for `IsAllPages` and the explicit list otherwise; `Resolve` throws when an explicit page exceeds `totalPages`.
- [ ] T005 Add nullable `PageSelection? PageSelection { get; set; }` (Cosmos JSON property `"pageSelection"`) to `Operation` in `src/DocumentOcr.Processor/Models/Operation.cs` per [data-model.md §2](data-model.md). Persist `null` for the "all pages" case.
- [ ] T006 [P] Update `tests/Models/OperationTests.cs` to add a round-trip test (Newtonsoft serialize → deserialize) covering both `PageSelection = null` and `PageSelection = TryParse("3-12, 15", null, ...)`. Existing tests must still pass.
- [ ] T007 [P] Add `string? PageRange { get; set; }` to `QueueMessage` in `src/DocumentOcr.Processor/Models/QueueMessage.cs` per [data-model.md §3](data-model.md) and [contracts/queue-message.md](contracts/queue-message.md). Add a unit test in `tests/Models/QueueMessageTests.cs` confirming default is `null` and that JSON without the field deserializes to `null` (back-compat).
- [ ] T008 [P] Add `string? PageRange { get; set; }` (with `[JsonPropertyName("pageRange")]`) to `StartOperationRequest` in `src/DocumentOcr.Processor/Functions/OperationsApi.cs` per [contracts/operations-api-start.md](contracts/operations-api-start.md). No behavioral change yet (wired in T013).
- [ ] T009 [P] Add `string? PageRange { get; set; }` to `OperationDto` in `src/DocumentOcr.WebApp/Models/OperationDto.cs` per [data-model.md §5](data-model.md).

**Checkpoint**: Solution builds (`dotnet build DocumentOcr.sln`) with zero new warnings; existing tests still pass; new `PageSelectionTests` and updated `OperationTests`/`QueueMessageTests` pass. No runtime behavior has changed yet.

---

## Phase 3: User Story 1 — Preview a PDF and select page ranges to process (Priority: P1) 🎯 MVP

**Goal**: A user can preview each selected PDF on the Upload page, type a print-dialog–style page range per file, submit, and the processor extracts OCR data only from the chosen pages.

**Independent Test**: Upload a multi-page PDF, enter `3-12, 15` in the new field, submit, and verify (a) the started operation persists `pageRange = "3-12, 15"`, (b) the worker calls `IDocumentIntelligenceService.AnalyzeDocumentAsync` exactly 11 times, (c) the resulting extracted documents do not contain content from the excluded pages.

### Tests for User Story 1

- [ ] T010 [P] [US1] Create `tests/Services/PdfProcessorPageRangeTests.cs`. Using mocks for `IPdfToImageService` (returns 20 fake `MemoryStream` pages), `IDocumentIntelligenceService`, `IDocumentAggregatorService`, `IBlobStorageService`, `ICosmosDbService`, `IOperationService`, `IImageToPdfService`, `IDocumentSchemaMapperService`, assert: (a) when `QueueMessage.PageRange = null`, `AnalyzeDocumentAsync` is called 20 times; (b) when `PageRange = "3-12, 15"`, it is called exactly 11 times and the per-call image is the expected page (verify via stream identity — the worker MUST index the existing `imageStreams` list by `selectedPage - 1` rather than re-decode); (c) when `PageRange = "25"` against a 20-page PDF, the operation transitions to `Failed` with an error mentioning bounds and `AnalyzeDocumentAsync` is never called; (d) **FR-011 / SC-006 invariant**: when `PageRange = "3-12, 15"`, the `pageNumber` values forwarded to each `PageOcrResult` are the document-local indices `1..11`, **not** the original PDF pages `3..15`.
- [ ] T011 [P] [US1] Add a unit test in `tests/Services/OperationsApiStartTests.cs` (new file) for `OperationsApi.StartOperation` covering: (a) `pageRange` omitted → operation persisted with `PageSelection == null` and queue message `PageRange == null`; (b) `pageRange = "3-12, 15"` → operation persisted with `PageSelection.Expression == "3-12, 15"` and `Pages.Count == 11`, queue message `PageRange == "3-12, 15"`; (c) `pageRange = "abc"` → `400 Bad Request` with the parser error in the body, no `Operation` created, no queue message sent. Mock `IOperationService` and `IQueueService`.

### Implementation for User Story 1

- [ ] T012 [US1] Modify `OperationsApi.StartOperation` in `src/DocumentOcr.Processor/Functions/OperationsApi.cs` per [contracts/operations-api-start.md](contracts/operations-api-start.md): after the existing required-field check, call `PageSelection.TryParse(startRequest.PageRange, maxPage: null, out var sel, out var err)`; on error return `400` with `err`; otherwise set `operation.PageSelection = sel.IsAllPages ? null : sel` before persist and set `queueMessage.PageRange = startRequest.PageRange` (raw string, may be `null`/empty).
- [ ] T013 [US1] Modify the `GetOperation` and `ListOperations` response builders in `src/DocumentOcr.Processor/Functions/OperationsApi.cs` to include `pageRange = operation.PageSelection?.Expression` (or `null`) per [contracts/operations-api-start.md](contracts/operations-api-start.md). Required by US3 too but added here so the Operations page can begin showing it as soon as US1 ships.
- [ ] T014 [US1] Modify `PdfProcessorFunction.Run` in `src/DocumentOcr.Processor/Functions/PdfProcessorFunction.cs` per [contracts/queue-message.md](contracts/queue-message.md): after Step 2 obtains `imageStreams.Count` (= `totalPages`), call `PageSelection.TryParse(message.PageRange, maxPage: totalPages, out var selection, out var err)`. On error: dispose `imageStreams`, mark operation `Failed` with `err`, return. On success: compute `selectedIndexes` and iterate the OCR loop only over those streams; dispose excluded streams up front. Preserve the existing `pageNumber = i + 1` assignment so per-document citations remain 1..N (FR-011).
- [ ] T015 [US1] Modify `OperationsApiService.StartOperationAsync` in `src/DocumentOcr.WebApp/Services/OperationsApiService.cs` and `IOperationsApiService` to add a `string? pageRange = null` parameter and include it in the JSON request body. Existing callers (default `null`) keep working unchanged.
- [ ] T016 [US1] Create `src/DocumentOcr.WebApp/Components/Shared/PdfRangePicker.razor` (and `.razor.cs` if needed). Component parameters: `IBrowserFile File`, `EventCallback<UploadFileEntry> OnEntryChanged`. Internals: load `/lib/pdfjs/pdf.mjs` via `IJSRuntime` lazy interop on first render; create an object URL from `File.OpenReadStream(MaxFileSize)`; render the **first page** in a `<canvas>` with previous/next buttons that re-render other pages on demand; expose `numPages` to component state; render a `<input class="form-control">` for the range expression and an inline `<small>` for the validation summary or error.
- [ ] T017 [US1] Create the JS interop module `src/DocumentOcr.WebApp/wwwroot/lib/pdfjs/range-picker.js` that wraps pdf.js: `loadDocument(arrayBuffer) -> { numPages, pageRendererId }`, `renderPage(rendererId, pageNumber, canvasElement)`, and `dispose(rendererId)`. Keep all pdf.js-specific code in this module (Constitution III: encapsulation).
- [ ] T018 [US1] Refactor `src/DocumentOcr.WebApp/Components/Pages/Upload.razor`: replace `List<IBrowserFile> selectedFiles` with `List<UploadFileEntry> selectedFiles` (per [data-model.md §6](data-model.md)). Render one `<PdfRangePicker>` per entry inside the existing `list-group`. In `UploadFiles()`, pass `entry.RangeExpression` to `OperationsApi.StartOperationAsync(blobName, containerName, pageRange: …)`. Submit button stays disabled when `selectedFiles.Any(e => e.RangeError is not null)` — full validation behavior is delivered in US2; for US1 the gating may simply require `entry.Selection is not null` after preview load.
- [ ] T019 [US1] In `Upload.razor` `HandleFilesSelected`, reject files for which the pdf.js page-count probe fails (corrupt/encrypted). Surface the existing `errorMessage` pattern; do not add the file to `selectedFiles`. Satisfies FR-015.

**Checkpoint**: Quickstart Scenarios A (default = all pages) and B (restricted range end-to-end) both pass. The MVP is shippable.

---

## Phase 4: User Story 2 — Validate the range expression before upload (Priority: P1)

**Goal**: Bad input is caught in the browser with clear inline errors before any upload begins. A normalized "X pages selected: …" summary is displayed for valid input. A per-batch "Apply to all" convenience exists for multi-file uploads (FR-010).

**Independent Test**: Type each invalid form (`abc`, `0`, `5-3`, out-of-bounds, `1-`) into the range field for a previewed PDF and confirm Submit stays disabled and the inline error matches the parser message; type `3-7, 5-10` and confirm summary reads `8 pages selected: 3–10`; click "Apply to all" with three files of different lengths and confirm bounds re-validate per file.

### Tests for User Story 2

- [ ] T020 [P] [US2] Add bUnit (or vanilla unit) test `tests/WebApp/PdfRangePickerTests.cs` (create folder if absent; if bUnit is not yet referenced in `tests/DocumentOcr.Tests.csproj`, add it). Cover: (a) entering `25-30` while `numPages=20` shows the bounds error and raises `OnEntryChanged` with `RangeError != null`; (b) entering `3-12, 15` updates the summary text to contain "11 pages" and "3" and "15"; (c) entering `   ` (whitespace) is treated as default → no error and summary indicates "All pages".
- [ ] T021 [P] [US2] Add `tests/WebApp/UploadPageGatingTests.cs` (bUnit) covering: with two files where one has an invalid range, the Upload button is `disabled`; clearing the bad range enables it.

### Implementation for User Story 2

- [ ] T022 [US2] In `PdfRangePicker.razor`, on every change of the range textbox, call the shared `PageSelection.TryParse(expression, maxPage: numPages, out var sel, out var err)`. Update `entry.RangeExpression`, `entry.Selection`, `entry.RangeError`, then invoke `OnEntryChanged`. Render either `<small class="text-danger">@RangeError</small>` or `<small class="text-muted">@Summary</small>` where `Summary` is built as `$"{sel.Pages.Count} pages selected: {FormatRanges(sel.Pages)}"` (a small helper that re-collapses contiguous runs back to `N–M` notation for display).
- [ ] T023 [US2] In `Upload.razor`, add an "Apply to all" `<button class="btn btn-link btn-sm">` shown only when `selectedFiles.Count > 1`, anchored to the first file's row. On click, copy `selectedFiles[0].RangeExpression` into every other entry's picker (via a public `Set(string expression)` method on `PdfRangePicker`), which triggers re-validation against each file's own `numPages`.
- [ ] T024 [US2] Tighten the Submit gate in `Upload.razor` to `selectedFiles.All(e => e.RangeError is null)` (already partially done in T018) and ensure the inline alert shown when blocked names the offending file(s).

**Checkpoint**: Quickstart Scenarios C (validation table) and D (Apply-to-all) pass. Combined with Phase 3, all P1 stories are complete.

---

## Phase 5: User Story 3 — Page-range information preserved end-to-end (Priority: P2)

**Goal**: The range used for an operation is visible on the Operations page and the Review page; a missing range displays as "All pages". (Citation numbering inside extracted documents is already 1..N per FR-011 — no code change needed there.)

**Independent Test**: Start an operation with `5-10`, open the Operations page and confirm the row/detail shows `Page range: 5-10`; open an extracted document on the Review page and confirm the displayed metadata shows `Page range: 5-10`. Start one without a range and confirm both pages show `Page range: All pages`.

### Tests for User Story 3

- [ ] T025 [P] [US3] Add a contract-style test in `tests/Services/OperationsApiGetTests.cs` (new file) confirming `GetOperation` includes `pageRange` in the JSON when set, and emits `null` (or absent — match the implementation choice) when `Operation.PageSelection` is `null`. Mock `IOperationService` to return both shapes.

### Implementation for User Story 3

- [ ] T026 [P] [US3] Update the Operations list/detail Razor pages under `src/DocumentOcr.WebApp/Components/Pages/` (the existing `Operations*.razor` files) to render a "Page range" cell/field. Use the shared display helper `dto.PageRange ?? "All pages"`. Reuse the existing badge/label vocabulary per Constitution III.
- [ ] T027 [P] [US3] Update the Review page `src/DocumentOcr.WebApp/Components/Pages/Review.razor` metadata panel to display the originating operation's `PageRange` (resolve via the existing operation fetch path; if not currently fetched on Review, add a single `OperationsApi.GetOperationAsync(documentResult.OperationId)` call gated by the `[Authorize]` attribute already in place). Display "All pages" when `null`/empty.
- [ ] T028 [P] [US3] Add a small UI helper `ReviewUiHelpers.FormatPageRange(string? pageRange)` in `src/DocumentOcr.WebApp/Services/ReviewUiHelpers.cs` returning `"All pages"` for null/empty and the raw expression otherwise. Use it in T026 and T027 to keep the rule in one place.

**Checkpoint**: Quickstart Scenarios E (back-compat with legacy ops) and F (citations remain 1..N) pass. All user stories complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T029 [P] Update `docs/WEB-APP-USAGE.md` with a "Selecting which pages to process" section showing the new Upload UI and the print-dialog grammar.
- [ ] T030 [P] Update `docs/OPERATIONS-API.md` to document the new optional `pageRange` field on `POST /api/operations` and on `GET /api/operations[/...]` responses, linking to [contracts/operations-api-start.md](contracts/operations-api-start.md).
- [ ] T031 [P] Update `docs/ARCHITECTURE.md` queue message description to mention the optional `PageRange` field, linking to [contracts/queue-message.md](contracts/queue-message.md).
- [ ] T032 [P] Update `docs/REVIEW-PAGE-UX.md` with the new "Page range" metadata field on the Review page (Constitution III mandates this update in the same PR).
- [ ] T033 Run `dotnet build DocumentOcr.sln` and confirm zero errors and zero new warnings (Constitution I).
- [ ] T034 Run `dotnet test` and confirm the full suite is green (Constitution II).
- [ ] T035 Walk through `quickstart.md` Scenarios A–G manually against a local Functions host + Azurite + WebApp; record any deviations as fixes before opening the PR.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: independent.
- **Foundational (Phase 2)**: independent of Phase 1; **blocks** all user-story phases.
- **User Story 1 (Phase 3)**: depends on Phase 2; depends on Phase 1 (T001) for the in-browser preview.
- **User Story 2 (Phase 4)**: depends on Phase 3 (extends `PdfRangePicker` and `Upload.razor`).
- **User Story 3 (Phase 5)**: depends on Phase 2 (for the persisted field) and on T013 (which exposes `pageRange` on `GET`); does **not** depend on Phase 4.
- **Polish (Phase 6)**: depends on all user-story phases being complete.

### Within each story

- Tests authored before or alongside implementation; for behavior-changing tasks (T012, T014) the corresponding test (T010, T011) MUST be in place and red before implementation lands (Constitution II red→green rule for new behavior).
- Models before services before endpoints before UI.

### Parallel opportunities

- T001 + T003 + T004 (different folders) can run in parallel.
- T005, T007, T008, T009 in Phase 2 are all in different files → all `[P]`-marked.
- Within Phase 3: T010 + T011 + T015 + T016 + T017 are in different files; T012 (Functions HTTP) and T014 (Functions worker) are in the same file but separate methods — sequence them rather than parallelize.
- Within Phase 5: T026, T027, T028 are in different files → all `[P]`.
- All Phase 6 doc tasks (T029–T032) are independent → all `[P]`.

---

## MVP scope

**Minimum shippable slice**: Phases 1 + 2 + 3 (User Story 1).

That alone delivers: preview, per-file range textbox (validation may be coarse — only "must parse" — until US2), end-to-end OCR restriction, persistence on `Operation`, and the `pageRange` field on the GET response (since T013 lives in US1). Everything afterward (US2 = polished validation UX + Apply-to-all; US3 = visible audit trail on Operations/Review) is incremental.

---

## Validation: format check

All tasks above conform to `- [ ] T### [P?] [Story?] Description with file path`:
- All have a checkbox.
- All have a sequential ID `T001`–`T035`.
- All Phase 3–5 tasks carry a `[US#]` label; Setup/Foundational/Polish tasks correctly omit it.
- All include an explicit file path or directory.
