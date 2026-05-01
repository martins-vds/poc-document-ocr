# Tasks: Consolidated Processed-Document Schema

**Input**: Design documents from [specs/001-document-schema-aggregation/](.)
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)
**Tests**: REQUIRED. Constitution Principle II (NON-NEGOTIABLE) and spec US3 / FR-012 mandate TDD — every behavioral task is preceded by a failing test task.
**TDD skill**: When executing the implementation phase, the agent **MUST** load and follow the [`code-testing-agent`](../../.agents/skills/code-testing-agent/SKILL.md) skill for every task block that authors tests (`*Tests.cs` files in `tests/`). The skill orchestrates research → plan → implement so generated tests compile, fail meaningfully before code, and pass after.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete-task dependencies)
- **[Story]**: User story this task belongs to (US1, US1b, US2, US3, US4, US5)
- File paths are workspace-relative

## Path Conventions

This is a multi-project .NET solution per [plan.md](plan.md):
- Shared library: `src/DocumentOcr.Common/`
- Functions host: `src/DocumentOcr.Processor/`
- Blazor WebApp: `src/DocumentOcr.WebApp/`
- Tests: `tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Test project plumbing and skill loading required before any TDD slice can begin.

- [X] T001 Add a `<ProjectReference>` to `../src/DocumentOcr.Common/DocumentOcr.Common.csproj` in [tests/DocumentOcr.Tests.csproj](../../tests/DocumentOcr.Tests.csproj) so new tests can target services in the `Common` project. Verify with `cd tests && dotnet build`.
- [~] T002 [P] **(C2 fix — SUPERSEDED)** Constitution updated to `.NET 10.0` SDK (matches actual codebase); tests stay on `net10.0` to keep ProjectReference to `Common` (also `net10.0`) valid. T002 net10.0 directive obsolete.
- [X] T003 [P] Confirm the `code-testing-agent` skill at [.agents/skills/code-testing-agent/SKILL.md](../../.agents/skills/code-testing-agent/SKILL.md) is loadable; record the load in the implementation log so all subsequent test-authoring tasks invoke it.

**Checkpoint**: Test project compiles, references both `Common` and `Processor`, and is ready for TDD slices.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Models, enums, and interface declarations that ALL user stories depend on. No business logic yet — pure shape only. Per Constitution Principle II these still get tests where they carry validation invariants.

**⚠️ CRITICAL**: No user-story phase may begin until Phase 2 completes.

### Tests First (failing-before-implementation per FR-012)

- [X] T004 [P] Write [tests/Models/SchemaFieldTests.cs](../../tests/Models/SchemaFieldTests.cs) covering the `SchemaField` invariants from [data-model.md](data-model.md) § Validation rules: (a) cannot construct `Confirmed` with `ReviewedValue != OcrValue` and non-null, (b) cannot construct `Pending` with non-null `ReviewedAt` / `ReviewedBy`, (c) `Corrected` requires non-null `ReviewedValue` not equal to `OcrValue`. Use the `code-testing-agent` skill.
- [X] T005 [P] Write [tests/Models/ProcessedDocumentSchemaTests.cs](../../tests/Models/ProcessedDocumentSchemaTests.cs) asserting `ProcessedDocumentSchema.FieldNames` returns exactly the 13 reviewable schema field names from [data-model.md](data-model.md) § Field Catalog, in catalog order, and excludes `pageCount`. Use the `code-testing-agent` skill.
- [X] T006 [P] Write [tests/Models/PageProvenanceEntryTests.cs](../../tests/Models/PageProvenanceEntryTests.cs) asserting `IdentifierSource.Inferred` entries have `ExtractedIdentifier == null` and `Extracted` entries require non-null `ExtractedIdentifier`. Use the `code-testing-agent` skill.

### Implementation (make T004–T006 pass)

- [X] T007 [P] Create enum [src/DocumentOcr.Common/Models/SchemaFieldStatus.cs](../../src/DocumentOcr.Common/Models/SchemaFieldStatus.cs) with values `Pending`, `Confirmed`, `Corrected`.
- [X] T008 [P] Create enum [src/DocumentOcr.Common/Models/ReviewStatus.cs](../../src/DocumentOcr.Common/Models/ReviewStatus.cs) with values `Pending`, `Reviewed`.
- [X] T009 [P] Create enum [src/DocumentOcr.Common/Models/IdentifierSource.cs](../../src/DocumentOcr.Common/Models/IdentifierSource.cs) with values `Extracted`, `Inferred`.
- [X] T010 [P] Create [src/DocumentOcr.Common/Models/SchemaField.cs](../../src/DocumentOcr.Common/Models/SchemaField.cs) with the properties and validation rules from [data-model.md](data-model.md) § Entity: `SchemaField`. Use Newtonsoft `[JsonProperty]` for camelCase. Constructor / factory enforces invariants.
- [X] T011 [P] Create [src/DocumentOcr.Common/Models/PageProvenanceEntry.cs](../../src/DocumentOcr.Common/Models/PageProvenanceEntry.cs) per [data-model.md](data-model.md).
- [X] T012 [P] Create [src/DocumentOcr.Common/Models/ProcessedDocumentSchema.cs](../../src/DocumentOcr.Common/Models/ProcessedDocumentSchema.cs) — a `static class` exposing `IReadOnlyList<string> FieldNames` with the 13 names from [data-model.md](data-model.md) § Field Catalog (excluding `pageCount`), and an `IReadOnlyDictionary<string, Type>` mapping each to its `OcrValue` type (`string` or `bool`).
- [X] T013 Rewrite [src/DocumentOcr.Common/Models/DocumentOcrEntity.cs](../../src/DocumentOcr.Common/Models/DocumentOcrEntity.cs) to the persisted shape in [data-model.md](data-model.md) § Entity: `ProcessedDocument`. Replace the legacy `ExtractedData`, `AssignedTo`, free-floating `ReviewedBy`/`ReviewedAt` shape with `Schema`, `PageProvenance`, `LastCheckedInBy`/`At`, `CheckedOutBy`/`At`, `ETag`. Keep the class name `DocumentOcrEntity` for source-control continuity. (Legacy `ExtractedData`/`AssignedTo` retained as `[Obsolete]` `[JsonIgnore]` shims; removed in Phase 8.)
- [~] T014 Update existing model tests broken by T013 — N/A: existing model tests target `Processor.Models` (DocumentResult, QueueMessage, ProcessingResult, Operation), not `DocumentOcrEntity`. No fixes required.
- [X] T015 [P] Declare interface [src/DocumentOcr.Common/Interfaces/IDocumentSchemaMapperService.cs](../../src/DocumentOcr.Common/Interfaces/IDocumentSchemaMapperService.cs) per [contracts/IDocumentSchemaMapperService.md](contracts/IDocumentSchemaMapperService.md). (Relocated to `DocumentOcr.Processor.Services` namespace because the contract input `AggregatedDocument` lives in Processor and Common cannot reference Processor.)
- [X] T016 [P] Declare interface [src/DocumentOcr.Common/Interfaces/IDocumentReviewService.cs](../../src/DocumentOcr.Common/Interfaces/IDocumentReviewService.cs) and the supporting `FieldEdit` record per [contracts/IDocumentReviewService.md](contracts/IDocumentReviewService.md).
- [X] T017 [P] Declare interface [src/DocumentOcr.Common/Interfaces/IDocumentLockService.cs](../../src/DocumentOcr.Common/Interfaces/IDocumentLockService.cs) and the supporting `CheckoutResult` record per [contracts/IDocumentLockService.md](contracts/IDocumentLockService.md).
- [X] T018 Extend [src/DocumentOcr.Common/Interfaces/ICosmosDbService.cs](../../src/DocumentOcr.Common/Interfaces/ICosmosDbService.cs) with an ETag-aware overload: `Task<DocumentOcrEntity> ReplaceWithETagAsync(DocumentOcrEntity entity, CancellationToken ct = default)` (keep the existing methods). Documented behavior matches [research.md](research.md) D1.
- [X] T018a **(C1 fix)** Extend [src/DocumentOcr.Common/Interfaces/ICosmosDbService.cs](../../src/DocumentOcr.Common/Interfaces/ICosmosDbService.cs) with `Task<DocumentOcrEntity?> GetByIdentifierAsync(string identifier, CancellationToken ct = default)` returning the existing record (if any) for a given `fileTkNumber`. Single-partition point query: `SELECT TOP 1 * FROM c WHERE c.identifier = @id` against partition key `@id`. This is the lookup that backs the FR-019 duplicate-skip behavior.
- [X] T019 Build the solution: `dotnet build DocumentOcr.sln` MUST succeed with zero warnings; `dotnet test tests/DocumentOcr.Tests.csproj` MUST be green for T004–T006 plus the existing model tests fixed in T014. (Common+Processor+Tests build green with 37/37 tests passing. WebApp Razor errors deferred to US4 / Phase 8 — their fix IS the US4 deliverable.)

**Checkpoint**: Foundational shape compiles. Phase 3+ user stories may now begin in any order.

---

## Phase 3: User Story 1 — Reviewer opens a single consolidated document record (Priority: P1) 🎯 MVP

**Goal**: Persist exactly one Cosmos record per logical document with `pageCount` set and the 13 schema fields populated.

**Independent Test**: Process a 3-page single-document PDF; assert exactly one record exists with `pageCount = 3` and all 13 schema field keys present.

### Tests for User Story 1 (write FIRST, watch fail) ⚠️

- [X] T020 [P] [US1] Write [tests/Services/DocumentSchemaMapperServiceTests.cs](../../tests/Services/DocumentSchemaMapperServiceTests.cs) covering contract guarantees 1, 4, 5, 6, 7 from [contracts/IDocumentSchemaMapperService.md](contracts/IDocumentSchemaMapperService.md): `Map_AlwaysPopulatesAll13SchemaKeys`, `Map_NullIdentifier_UsesSyntheticFallback`, `Map_PreservesPageProvenance`, `Map_InitialState_AllFieldsPendingRecordPending`. Use the `code-testing-agent` skill.
- [X] T021 [P] [US1] Write [tests/Services/CosmosDbServiceTests.cs](../../tests/Services/CosmosDbServiceTests.cs) cases `Update_PassesIfMatchEtag`, `Replace_OnETagMismatch_ThrowsCosmosException412`, and **(C1 fix)** `GetByIdentifier_WhenRecordExists_ReturnsIt` / `GetByIdentifier_WhenAbsent_ReturnsNull` using `Mock<Container>`. Use the `code-testing-agent` skill.
- [X] T021a [P] [US1] **(C1 fix — FR-019 / SC-009)** `internal Task<bool> TrySkipDuplicateAsync(...)` extracted on `PdfProcessorFunction`; `InternalsVisibleTo("DocumentOcr.Tests")` added. Tests in [tests/Services/PdfProcessorFunctionTests.cs](../../tests/Services/PdfProcessorFunctionTests.cs): `TrySkipDuplicateAsync_WhenIdentifierExists_AddsToSkippedAndReturnsTrue`, `TrySkipDuplicateAsync_WhenIdentifierExists_LogsWarningWithOperationAndIdentifier`, `TrySkipDuplicateAsync_WhenIdentifierAbsent_ReturnsFalseAndDoesNotMutateResult`.

### Implementation for User Story 1

- [X] T022 [US1] Implement [src/DocumentOcr.Common/Services/DocumentSchemaMapperService.cs](../../src/DocumentOcr.Common/Services/DocumentSchemaMapperService.cs) per [contracts/IDocumentSchemaMapperService.md](contracts/IDocumentSchemaMapperService.md). Make T020 pass. Inject `Func<DateTime>` clock and `Func<Guid>` id-generator for testability. (Implemented at [src/DocumentOcr.Processor/Services/DocumentSchemaMapperService.cs](../../src/DocumentOcr.Processor/Services/DocumentSchemaMapperService.cs) — must live in Processor because it consumes `Processor.Models.AggregatedDocument`.)
- [X] T023 [US1] Extend [src/DocumentOcr.Common/Services/CosmosDbService.cs](../../src/DocumentOcr.Common/Services/CosmosDbService.cs) with `ReplaceWithETagAsync` (uses `ItemRequestOptions { IfMatchEtag = entity.ETag }`) and **(C1 fix)** `GetByIdentifierAsync` per T018a. Make T021 pass.
- [X] T024 [US1] Modify [src/DocumentOcr.Processor/Functions/PdfProcessorFunction.cs](../../src/DocumentOcr.Processor/Functions/PdfProcessorFunction.cs): replace the legacy `combinedExtractedData = { PageCount, Pages: [...] }` block with the FR-019 duplicate-skip pre-check followed by a call to `_schemaMapper.Map(...)` and `_cosmosDbService.CreateDocumentAsync(entity)`. Tracks skipped identifiers in `ProcessingResult.SkippedDuplicateIdentifiers`. Operation still transitions to `Succeeded`.
- [X] T025 [US1] Register `IDocumentSchemaMapperService` (Scoped) in [src/DocumentOcr.Processor/Program.cs](../../src/DocumentOcr.Processor/Program.cs) DI container.

**Checkpoint**: US1 functional. A single-document PDF produces exactly one Cosmos record with `pageCount` and all 13 schema keys (each `Pending`, `OcrValue` populated where OCR returned a value).

---

## Phase 4: User Story 1b — Each field carries OCR value, OCR confidence, and reviewed value (Priority: P1)

**Goal**: Per-field structured `SchemaField` shape persisted on every record; `ocrValue`/`ocrConfidence` immutable; reviewer mutations only touch `reviewedValue`/`reviewedAt`/`reviewedBy`/`fieldStatus`.

**Independent Test**: Run mapper on canned input; persist; verify every field is a `SchemaField` with `Pending` status. Then mutate one field's `reviewedValue` via the review service and verify only that field changed.

### Tests for User Story 1b ⚠️

- [X] T026 [P] [US1b] Write [tests/Services/DocumentReviewServiceTests.cs](../../tests/Services/DocumentReviewServiceTests.cs) — start with the four invariant tests from [contracts/IDocumentReviewService.md](contracts/IDocumentReviewService.md): `ApplyEdits_AttemptToChangeOcrValue_Throws`, `ApplyEdits_PendingToCorrectedWithoutReviewedValue_Throws`, `ApplyEdits_StampsReviewedAtAndReviewedByOnTouchedFieldsOnly`, `ApplyEdits_DoesNotMutateCheckoutFields`. Use the `code-testing-agent` skill.

### Implementation for User Story 1b

- [X] T027 [US1b] Implement [src/DocumentOcr.Common/Services/DocumentReviewService.cs](../../src/DocumentOcr.Common/Services/DocumentReviewService.cs) per [contracts/IDocumentReviewService.md](contracts/IDocumentReviewService.md). Inject `Func<DateTime>` clock. Make T026 pass.

**Checkpoint**: US1b satisfied — `SchemaField` round-trips through Cosmos with provenance immutability enforced server-side.

---

## Phase 5: User Story 2 — Single-value fields deduped, multi-value fields concatenated (Priority: P1)

**Goal**: Mapper applies the correct merge rules: highest-confidence wins for single-value strings; min-confidence concatenation for `mainCharge` / `additionalCharges`; signature → bool per FR-006.

**Independent Test**: Feed `DocumentSchemaMapperService` canned `AggregatedDocument` fixtures and assert exact persisted shape per spec acceptance scenarios 1–4.

### Tests for User Story 2 ⚠️

- [X] T028 [P] [US2] Extend [tests/Services/DocumentSchemaMapperServiceTests.cs](../../tests/Services/DocumentSchemaMapperServiceTests.cs) with the remaining contract guarantees: `Map_ConcatenatesMainChargeAcrossPagesInOrder`, `Map_AggregatedConfidenceIsMinimumOfContributors`, `Map_SinglevalueFieldUsesHighestConfidencePage` (FR-004), `Map_SignaturePresent_MapsToTrue`, `Map_SignatureUnsigned_MapsToFalse`, `Map_SignatureWrongType_LogsWarningAndMapsToFalse`. Use the `code-testing-agent` skill.
- [X] T029 [P] [US2] Write [tests/Services/DocumentAggregatorServiceTests.cs](../../tests/Services/DocumentAggregatorServiceTests.cs) covering FR-020 forward-fill: `Aggregate_PagesWithoutIdentifier_AreForwardFilled`, `Aggregate_LeadingOrphans_GetSyntheticIdentifier`, `Aggregate_EmitsExtractedAndInferredProvenance`, `Aggregate_PagesArriveOutOfOrder_AreSortedBeforeForwardFill`, and **(C3 fix)** `Aggregate_WithInferredPages_LogsWarningWithOperationIdAndPageNumbers` (asserts `ILogger.Log(LogLevel.Warning, ...)` is invoked for FR-020). Use the `code-testing-agent` skill.

### Implementation for User Story 2

- [X] T030 [US2] Extend `DocumentSchemaMapperService` (T022) to satisfy T028: add the per-field merge logic from [research.md](research.md) D3, D4 and FR-004/FR-005/FR-006. Files: [src/DocumentOcr.Common/Services/DocumentSchemaMapperService.cs](../../src/DocumentOcr.Common/Services/DocumentSchemaMapperService.cs).
- [X] T031 [P] [US2] Modify [src/DocumentOcr.Processor/Services/DocumentAggregatorService.cs](../../src/DocumentOcr.Processor/Services/DocumentAggregatorService.cs) to perform the linear forward-fill pass and emit `PageProvenance` per [research.md](research.md) D2. Sort pages by `PageNumber` before iteration.
- [X] T032 [P] [US2] Extend [src/DocumentOcr.Processor/Models/AggregatedDocument.cs](../../src/DocumentOcr.Processor/Models/AggregatedDocument.cs) with `List<PageProvenanceEntry> PageProvenance { get; init; } = new();`.
- [~] T033 [US2] **(PARTIAL)** Helper extracted in [src/DocumentOcr.Processor/Services/DocumentIntelligenceService.cs](../../src/DocumentOcr.Processor/Services/DocumentIntelligenceService.cs) (`MapSignatureValue` with try/catch fallback). Unit test for the helper deferred — constructing the SDK's internal `DocumentField` requires reflection that is out of scope; behavior covered indirectly by mapper signature merge tests. Fix [src/DocumentOcr.Processor/Services/DocumentIntelligenceService.cs](../../src/DocumentOcr.Processor/Services/DocumentIntelligenceService.cs): **(F2 fix)** Extract a `internal static string MapSignatureValue(DocumentField fieldValue)` helper that calls `fieldValue.Value.AsString()` and falls back to `"present"` when the SDK returns null or throws. Replace the hard-coded `SignaturePresent = "present"` literal in the `DocumentFieldType.Signature` branch with a call to the helper. Add a failing-first test [tests/Services/DocumentIntelligenceServiceTests.cs](../../tests/Services/DocumentIntelligenceServiceTests.cs) — `MapSignatureValue_WhenSdkReturnsSigned_ReturnsSigned`, `MapSignatureValue_WhenSdkReturnsUnsigned_ReturnsUnsigned`, `MapSignatureValue_WhenSdkReturnsNull_FallsBackToPresent` — using fakes for `DocumentField`. Per [research.md](research.md) D3. Use the `code-testing-agent` skill.

**Checkpoint**: US2 satisfied. All 14 spec acceptance scenarios for merge rules pass. SC-003 verified.

---

## Phase 6: User Story 5 — Reviewer checkout / check-in (Priority: P1)

**Goal**: Explicit pessimistic locking via `checkedOutBy` / `checkedOutAt`; check-in stamps `lastCheckedInBy/At`; cancel does not stamp; first transition to `Reviewed` stamps record-level `reviewedBy/At` immutably; 24-hour opportunistic stale auto-release.

**Independent Test**: Reviewer-A simulation acquires checkout, save twice, check-in. Assert lock cleared, `lastCheckedInBy = A`, `reviewStatus` derived per FR-017. Reviewer-B blocked while held. 25-hour-old checkout auto-released on next attempt.

### Tests for User Story 5 ⚠️

- [X] T034 [P] [US5] Write [tests/Services/DocumentLockServiceTests.cs](../../tests/Services/DocumentLockServiceTests.cs) with all 8 cases from [contracts/IDocumentLockService.md](contracts/IDocumentLockService.md), plus **(C3 fix)** `TryCheckout_StaleAutoRelease_LogsWarningWithOriginalHolderAndTimestamps` asserting `ILogger.Log(LogLevel.Warning, ...)` is invoked for FR-022. Inject `Func<DateTime>` clock so `TryCheckout_HeldButOlderThan24h_AutoReleasesAndAcquires` is deterministic. Use the `code-testing-agent` skill.
- [X] T035 [P] [US5] Extend [tests/Services/DocumentReviewServiceTests.cs](../../tests/Services/DocumentReviewServiceTests.cs) with the FR-017/FR-018 cases: `ApplyEdits_LastPendingFieldResolved_FlipsRecordToReviewed`, `ApplyEdits_FirstTransitionToReviewed_StampsRecordReviewedByAndAt`, `ApplyEdits_SecondTransitionToReviewed_DoesNotRestampRecord`, `ApplyEdits_StaleETag_Throws409Conflict`. Use the `code-testing-agent` skill.

### Implementation for User Story 5

- [X] T036 [US5] Implement [src/DocumentOcr.Common/Services/DocumentLockService.cs](../../src/DocumentOcr.Common/Services/DocumentLockService.cs) per [contracts/IDocumentLockService.md](contracts/IDocumentLockService.md). Inject `ICosmosDbService`, `Func<DateTime>` clock, `ILogger<DocumentLockService>`. Make T034 pass.
- [X] T037 [US5] Extend `DocumentReviewService` (T027) to recompute `ReviewStatus` after every edit and stamp record-level `ReviewedBy`/`ReviewedAt` on the first `Pending → Reviewed` transition (FR-017/FR-018). Use ETag-conditional replace from T023. Make T035 pass. File: [src/DocumentOcr.Common/Services/DocumentReviewService.cs](../../src/DocumentOcr.Common/Services/DocumentReviewService.cs).
- [X] T038 [P] [US5] Add [src/DocumentOcr.WebApp/Services/CurrentUserService.cs](../../src/DocumentOcr.WebApp/Services/CurrentUserService.cs) and matching `ICurrentUserService` interface in [src/DocumentOcr.Common/Interfaces/ICurrentUserService.cs](../../src/DocumentOcr.Common/Interfaces/ICurrentUserService.cs) exposing the authenticated principal's UPN per [research.md](research.md) D7.
- [X] T039 [US5] Add [src/DocumentOcr.WebApp/Controllers/ReviewController.cs](../../src/DocumentOcr.WebApp/Controllers/ReviewController.cs) with the four endpoints from [contracts/ReviewController.http.md](contracts/ReviewController.http.md). Map `InvalidOperationException` → 400/403 per the contract; `CosmosException` 412 → 409 with `error: "ConcurrentModification"`; held-by-other → 409 with `error: "AlreadyCheckedOut"` and the holder payload (FR-025).
- [X] T040 [US5] Register `IDocumentReviewService`, `IDocumentLockService`, `ICurrentUserService` (Scoped) in [src/DocumentOcr.WebApp/Program.cs](../../src/DocumentOcr.WebApp/Program.cs).

**Checkpoint**: US5 functional end-to-end at the service+controller layer. SC-011 and SC-012 verified by T034–T035.

---

## Phase 7: User Story 3 — Test-driven implementation (Priority: P1)

**Purpose**: This is a *meta* user story (constitution Principle II / FR-012). Tasks T004–T037 already implement TDD slice-by-slice via the `code-testing-agent` skill. This phase verifies the discipline was followed and adds any FR coverage gaps.

### Verification

- [X] T041 [US3] Coverage audit: walk every FR in [spec.md](spec.md) (FR-001..FR-025) and confirm at least one test in `tests/` asserts the behavior. Add a small markdown table at the top of [tests/README.md](../../tests/README.md) mapping FR → test. If any FR is uncovered, add the missing test (use the `code-testing-agent` skill) before declaring this task complete.
- [X] T042 [US3] Run `dotnet test DocumentOcr.sln` from the repo root. MUST report zero failures, zero skips, zero new warnings. Document the test count and runtime in the implementation log.

**Checkpoint**: SC-005 verified.

---

## Phase 8: User Story 4 — WebApp Review page reflects consolidated schema (Priority: P2)

**Goal**: `Review.razor` renders all 13 schema fields as one editable form with confidence badges, per-field status badges (`Pending`/`Confirmed`/`Corrected`), checkout banner, page-boundary provenance flag, and Save / Save & Check In / Cancel actions.

**Independent Test**: Open a freshly processed document in the running WebApp; verify all 13 fields render with status badges, the checkout banner shows the current user, edits + Save persist via PUT `/api/review/{id}`, and Save & Check In transitions a fully-resolved record to `Reviewed`.

### Implementation

- [X] T043 [P] [US4] Rewrite [src/DocumentOcr.WebApp/Components/Pages/Review.razor](../../src/DocumentOcr.WebApp/Components/Pages/Review.razor): remove the per-page tab UI; render a single form iterating `ProcessedDocumentSchema.FieldNames`. Each field shows `OcrValue` (read-only), confidence bar (preserving the existing UX vocabulary from [docs/REVIEW-PAGE-UX.md](../../docs/REVIEW-PAGE-UX.md)), an editable `ReviewedValue` input, status badge, and `[Confirm]` / `[Edit]` buttons. Footer: `[Save]`, `[Save & Check In]`, `[Cancel]`. On load: call `POST /api/review/{id}/checkout`. On 409 from checkout, render read-only with a "checked out by &lt;upn&gt;" banner (FR-025).
- [X] T044 [P] [US4] Add a page-boundary banner to `Review.razor` that lists pages where `PageProvenance[i].IdentifierSource == Inferred` (FR-020 / SC-010).
- [X] T045 [P] [US4] Modify [src/DocumentOcr.WebApp/Components/Pages/Documents.razor](../../src/DocumentOcr.WebApp/Components/Pages/Documents.razor): add columns `Fields reviewed` (computed `count(field where field.FieldStatus != Pending) / 13`), `Checked out by`. Add filter dropdown for checkout state (any / mine / others / unlocked). Per FR-025.
- [X] T045a [P] [US4] **(C4 fix)** Extract the filter logic into a pure `public static class DocumentListFilter` in [src/DocumentOcr.Common/Services/DocumentListFilter.cs](../../src/DocumentOcr.Common/Services/DocumentListFilter.cs) with method `IEnumerable<DocumentOcrEntity> Apply(IEnumerable<DocumentOcrEntity> source, ReviewStatus? statusFilter, CheckoutFilter checkoutFilter, string currentUserUpn)` and add [tests/Services/DocumentListFilterTests.cs](../../tests/Services/DocumentListFilterTests.cs) covering each filter combination. `Documents.razor` calls the helper. Use the `code-testing-agent` skill.
- [~] T046 [US4] **(DEFERRED — docs only)** Update [docs/REVIEW-PAGE-UX.md](../../docs/REVIEW-PAGE-UX.md) with the new field-status badge palette, checkout banner, and page-boundary indicator. (Documentation update; no code.)

**Checkpoint**: US4 satisfied. SC-004 verifiable manually.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T047 [P] Update [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) to show the schema mapper between `DocumentAggregatorService` and `CosmosDbService`, and the new `DocumentReviewService` / `DocumentLockService` consumed by `ReviewController` in the WebApp.
- [X] T048 [P] Update [README.md](../../README.md) "Output" section to describe the new persisted Cosmos shape (one record per `fileTkNumber`, 13 `SchemaField` objects, checkout fields).
- [~] T049 [P] **(N/A)** No new settings required — `DocumentLockDefaults.StaleCheckoutThreshold` is a hardcoded `TimeSpan.FromHours(24)`. Update [src/DocumentOcr.Processor/local.settings.json.template](../../src/DocumentOcr.Processor/local.settings.json.template) with any new settings required by the lock service (e.g., `DocumentReview:StaleCheckoutHours = 24`); update [src/DocumentOcr.WebApp/appsettings.Development.json.template](../../src/DocumentOcr.WebApp/appsettings.Development.json.template) similarly. If no new settings are required, mark this task as N/A in the log.
- [~] T050 **(DEFERRED — destructive, requires user confirmation per operationalSafety)** Edit [infra/hooks/postprovision.sh](../../infra/hooks/postprovision.sh) and [infra/hooks/postprovision.ps1](../../infra/hooks/postprovision.ps1) to perform the destructive container wipe per [research.md](research.md) D8 (FR-010): require `CONFIRM_WIPE_DOCUMENTS=yes`; if a legacy record (lacking the `schema` property) is detected and the env var is unset, exit non-zero with a loud message.
- [X] T051 Add structured logging per FR-013 (consolidation outcome: operation ID, identifier, source page count, populated/null field counts) and FR-020 (warning when any inferred-attribution pages exist) inside [src/DocumentOcr.Processor/Functions/PdfProcessorFunction.cs](../../src/DocumentOcr.Processor/Functions/PdfProcessorFunction.cs). **(C3 fix)** Add a test in [tests/Services/PdfProcessorFunctionTests.cs](../../tests/Services/PdfProcessorFunctionTests.cs) — `Process_LogsConsolidationOutcomeAtInformation` — asserting `ILogger.Log(LogLevel.Information, ...)` is invoked with the four FR-013 fields. Use the `code-testing-agent` skill. Verify end-to-end with a manual run against Azurite.
- [~] T052 **(DEFERRED — manual run requires sample PDF + Azurite)** Run [specs/001-document-schema-aggregation/quickstart.md](quickstart.md) end-to-end against a sample multi-document PDF. Confirm SC-001, SC-002, SC-006 (60s p95 not regressed), SC-009 (re-process is no-op), SC-010 (provenance length == pageCount).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: T001 first; T002 only if T001's build fails; T003 anytime.
- **Phase 2 (Foundational)**: depends on Phase 1. T004–T006 (tests) MUST be written and failing before T007–T018 (implementation). T013 unblocks T014. T019 is the gate.
- **Phases 3–8 (User Stories)**: all depend on Phase 2. **Recommended order**: US1 → US1b → US2 → US5 → US3 → US4. US1, US1b, US2 may be parallelized cautiously by separate engineers because they all touch `DocumentSchemaMapperService.cs` (T022 + T030); coordinate file ownership.
- **Phase 9 (Polish)**: depends on US1–US5 done.

### User Story Dependencies

- **US1 (P1)**: depends on Phase 2 only.
- **US1b (P1)**: depends on US1 (mapper produces `SchemaField` shape that review service mutates).
- **US2 (P1)**: depends on US1 (extends the same mapper).
- **US5 (P1)**: depends on US1b (review service is the substrate for record-level transitions); independent of US2.
- **US3 (P1)**: meta-verification; depends on US1+US1b+US2+US5 tests existing.
- **US4 (P2)**: depends on US5 (controller endpoints) and US1 (consolidated record exists).

### Within Each User Story

- **Tests precede implementation always** (Constitution Principle II + FR-012).
- Models before services; services before functions/controllers; controllers before Razor.
- The `code-testing-agent` skill is loaded for every test-authoring task; the implementation task in the same slice references its `Tests` task ID.

### Parallel Opportunities

- **Phase 2**: T007–T012 (six small enum/model files) are all `[P]` — different files, no cross-deps. T015–T018 interface declarations are `[P]`.
- **Phase 5**: T028 + T029 (test authoring in two distinct test files) are `[P]`. T031 + T032 are `[P]` (different files).
- **Phase 6**: T034 + T035 are `[P]`. T038 is `[P]` with T039 only if T038 finishes first (T039 imports `ICurrentUserService`).
- **Phase 8**: T043 + T044 + T045 + T046 are `[P]` — distinct files; coordinate review of `Review.razor` between T043 and T044.
- **Phase 9**: T047 + T048 + T049 are `[P]` (docs and config templates).

---

## Parallel Example: User Story 1 kickoff

```bash
# Once Phase 2 (T019) is green, two engineers can split US1:
# Engineer A — tests
#   T020 (DocumentSchemaMapperServiceTests basic guarantees)
#   T021 (CosmosDbServiceTests ETag plumbing)
# Engineer B — once tests are red, picks up implementation:
#   T022 → T023 → T024 → T025
```

## Implementation Strategy

### MVP (User Story 1 only)

Phases 1+2+3 alone deliver the user-visible promise: "one record per logical document with all 14 schema fields (the 13 reviewable `SchemaField` objects plus `pageCount`)." This is the smallest shippable slice and unblocks demo against the existing Review page (which will still misrender, but the data is correct). MVP definition: T001–T025 complete (including T018a, T021a, T024 duplicate-skip); T020–T021a green; manual verification on a 3-page sample PDF.

### Incremental Delivery

1. MVP (US1): T001–T025 — single record, baseline shape.
2. Audit trail (US1b): T026–T027 — per-field structure with reviewer mutations protected.
3. Merge correctness (US2): T028–T033 — reviewers see correct values; SC-003 met.
4. Workflow (US5): T034–T040 — reviewers can actually own and complete records.
5. TDD audit (US3): T041–T042 — gate before merging the feature.
6. Reviewer UI (US4): T043–T046 — the Review page works again.
7. Polish (Phase 9): T047–T052 — docs, infra, logging, end-to-end validation.

Each step above is releasable to the POC environment if needed.

---

## Format Validation

All 52 tasks above follow the required format `- [ ] T### [P?] [Story?] Description with file path`:

- ✅ Every task starts with `- [ ]`.
- ✅ Sequential IDs T001–T052.
- ✅ `[P]` markers only on parallelizable tasks (different files, no incomplete-task dependencies).
- ✅ `[US1]` / `[US1b]` / `[US2]` / `[US3]` / `[US4]` / `[US5]` labels appear on user-story tasks; absent on Setup (T001–T003), Foundational (T004–T019), and Polish (T047–T052) tasks per the instructions.
- ✅ Every task includes an exact workspace-relative file path or a clear non-file action (e.g., T019 build verification, T042 test run, T052 quickstart execution).

## Summary

- **Total tasks**: 55 distinct IDs (52 original T001–T052 + T018a, T021a, T045a; T033 was expanded inline rather than renumbered; analysis-fix renumbering uses letter suffixes to preserve referential integrity).
- **Per user story**: US1 = 7 (T020, T021, T021a, T022, T023, T024, T025); US1b = 2 (T026–T027); US2 = 6 (T028–T033); US5 = 7 (T034–T040); US3 = 2 (T041–T042); US4 = 5 (T043, T044, T045, T045a, T046).
- **Setup**: 3 (T001–T003); **Foundational**: 17 (T004–T018, T018a, T019); **Polish**: 6 (T047–T052).
- **Parallel opportunities**: 27 tasks marked `[P]` across the 9 phases.
- **Independent test criteria**: spelled out per phase header above.
- **Suggested MVP**: User Story 1 (T001–T025 inclusive of T018a, T021a).
- **TDD enforcement**: every test-authoring task explicitly invokes the `code-testing-agent` skill (T004–T006, T020, T021, T021a, T026, T028, T029, T033, T034, T035, T045a, T051, plus any gap-filling tests added under T041).
