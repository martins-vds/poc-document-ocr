# Feature Specification: Consolidated Processed-Document Schema

**Feature Branch**: `001-document-schema-aggregation`
**Created**: 2026-05-01
**Status**: Draft (clarifications resolved)
**Input**: User description: "Implement the feature specification based on the updated constitution. I want the processor to generate a document with the following schema: fileTkNumber → string, pageNumber → counted as pageCount, criminalCodeForm → string, policeFileNumber → string, agency → string, accusedSex → string, accusedName → string, accusedDatefBirth → string, mainCharge → string (concatenated across pages), signedOn → string, judgeSignature → bool, endorsementSignature → bool, endorsementSignedOn → string, additionalCharges → string (concatenated across pages). The processor currently persists all pages of a document in the database; this must change so the database stores the processed (consolidated) document. A physical document can span multiple pages."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reviewer opens a single consolidated document record (Priority: P1)

A reviewer opens an entry in the documents list and sees one record per physical document — not one record per page. The record contains the canonical, business-meaningful fields (file number, accused name, charges, signatures, etc.) already merged across all pages of that document, with the total page count clearly displayed.

**Why this priority**: This is the core outcome the user requested. Today the database holds per-page noise; reviewers cannot answer "what does this document say?" without mentally merging pages. Without this story the feature delivers no value.

**Independent Test**: Process a multi-page sample PDF (3 pages, 1 logical document) through the pipeline. Open the corresponding record in the database (or via the WebApp document list). Verify the record contains exactly one entry for that document, populated with the 14 schema fields and a `pageCount` of 3.

**Acceptance Scenarios**:

1. **Given** a 3-page PDF representing one logical document, **When** the processor finishes, **Then** exactly one record exists in the documents container for that document, with `pageCount = 3` and all schema fields populated from the source pages.
2. **Given** a multi-document PDF where pages 1–2 belong to document A and pages 3–4 to document B, **When** the processor finishes, **Then** exactly two records exist, each with `pageCount = 2` and fields drawn only from its own pages.
3. **Given** a document where `mainCharge` appears on pages 1 and 3, **When** the consolidated record is produced, **Then** the stored `mainCharge` value contains the page-1 content followed by the page-3 content, in page order, and its aggregated confidence is the **minimum** of the contributing pages' confidence values.

---

### User Story 1b - Each field carries OCR value, OCR confidence, and reviewed value (Priority: P1)

Every schema field in the persisted record is itself a small structure that records what Document Intelligence originally extracted (`ocrValue`, `ocrConfidence`), what the reviewer (if any) has set it to (`reviewedValue`), and the per-field review state (`Pending` / `Confirmed` / `Corrected`). An "unreviewed document" is simply a record where every field's status is `Pending`; a "reviewed document" is one where every field's status is `Confirmed` or `Corrected`. There is one schema, two end-states.

**Why this priority**: Without the OCR-vs-reviewed split, the first reviewer edit destroys the audit trail and removes our only way to measure model accuracy. Without per-field state, the system cannot tell whether a reviewer actually examined a field or just clicked Save. Both are required for trustworthy legal-record handling. Co-equal P1.

**Independent Test**: Persist a freshly processed record and verify each schema field carries `ocrValue`, `ocrConfidence`, `reviewedValue = null`, and `fieldStatus = "Pending"`. Then simulate a reviewer correction on one field and verify only that field's `reviewedValue`, `reviewedAt`, `reviewedBy`, and `fieldStatus = "Corrected"` change; `ocrValue` and `ocrConfidence` remain untouched.

**Acceptance Scenarios**:

1. **Given** a freshly processed document, **When** persisted, **Then** every schema field has `ocrValue` set, `ocrConfidence` set (or `null` for `pageCount` and for fields whose underlying OCR did not report a confidence), `reviewedValue = null`, `reviewedAt = null`, `reviewedBy = null`, and `fieldStatus = "Pending"`; the record-level `reviewStatus` is `Pending`.
2. **Given** a reviewer changes `accusedName` from `"John Smith"` to `"John A. Smith"` and confirms `fileTkNumber` without changing it, **When** the change is saved, **Then** `accusedName.reviewedValue = "John A. Smith"`, `accusedName.fieldStatus = "Corrected"`, `fileTkNumber.reviewedValue = fileTkNumber.ocrValue`, `fileTkNumber.fieldStatus = "Confirmed"`, all other fields remain `Pending`, and the record-level `reviewStatus` remains `Pending` (because not every field has been touched yet).
3. **Given** every field on a record has `fieldStatus` of `Confirmed` or `Corrected`, **When** the record is loaded, **Then** the record-level `reviewStatus` is `Reviewed`.
4. **Given** any reviewer edit, **When** persisted, **Then** the field's `ocrValue` and `ocrConfidence` are byte-for-byte unchanged from the original extraction.

---

### User Story 2 - Single-value fields are deduplicated, multi-value fields are concatenated (Priority: P1)

For every field in the schema, the system applies the correct merge rule: single-value fields (e.g., `fileTkNumber`, `accusedName`) take one canonical value; the two listed multi-value fields (`mainCharge`, `additionalCharges`) are concatenated in page order; signature fields are coerced to booleans; `pageNumber` is collapsed into a `pageCount` integer.

**Why this priority**: The merge rules are the substantive logic of the feature. Getting them wrong silently corrupts legal-record data and undermines reviewer trust. Co-equal P1 with Story 1.

**Independent Test**: Feed the aggregator a fixed, in-memory set of per-page OCR results covering each schema field with known values across pages and assert the consolidated output matches the expected record exactly. No Azure dependencies required.

**Acceptance Scenarios**:

1. **Given** `fileTkNumber = "TK-2024-001"` appears on pages 1, 2, and 3, **When** consolidation runs, **Then** the record stores `fileTkNumber = "TK-2024-001"` exactly once (no duplication, no array).
2. **Given** `judgeSignature` is returned by Document Intelligence with `type = "signature"` and `valueSignature = "signed"` (confidence `0.995`) on at least one page, **When** consolidation runs, **Then** the record stores `judgeSignature.ocrValue = true` and `judgeSignature.ocrConfidence = 0.995`. **Given** the field is absent or returned with any other shape on every page, **Then** `judgeSignature.ocrValue = false` and `judgeSignature.ocrConfidence = null`.
3. **Given** `additionalCharges` appears on pages 2 and 3 with text "Charge X" and "Charge Y" respectively, **When** consolidation runs, **Then** the record stores the concatenated value in page order.
4. **Given** the source pages are 1, 2, and 3, **When** consolidation runs, **Then** `pageCount = 3` and no `pageNumber` field exists in the record.

---

### User Story 3 - Test-driven implementation (Priority: P1)

Per the constitution (Principle II, NON-NEGOTIABLE), the feature is delivered TDD: failing unit tests for the new aggregator behavior and the new persistence shape are written first, then implementation makes them pass. No production code lands without an accompanying failing-then-passing test.

**Why this priority**: Mandated by the constitution and explicitly requested by the user. Without it, the feature cannot be merged.

**Independent Test**: Inspect the PR history / commits: tests for each acceptance scenario above must appear in `tests/` and must have failed before the corresponding production code was added. `dotnet test` passes on the final commit.

**Acceptance Scenarios**:

1. **Given** the new aggregator behavior, **When** the test suite is run before implementation, **Then** the new tests fail with clear assertion messages identifying the missing behavior.
2. **Given** the implementation is complete, **When** `dotnet test` is run, **Then** all existing and new tests pass with zero skips and zero new warnings.

---

### User Story 4 - WebApp Review page reflects consolidated schema (Priority: P2)

A reviewer opening the Review page sees the 14 schema fields as a single editable form (not a tabbed page-by-page view), with confidence indicators preserved per field per the existing UX vocabulary (`docs/REVIEW-PAGE-UX.md`).

**Why this priority**: The current Review page (`Review.razor`) parses a `Pages[]` array from `extractedData` to render per-page tabs. Once the database stores the consolidated schema, the existing tabbed UI will have nothing to render. This story keeps the reviewer experience usable. P2 because, in principle, the database change can ship before the UI change if a stop-gap is acceptable — but see Clarification Q1.

**Independent Test**: Open a freshly processed document in the WebApp. Verify all 14 schema fields are visible, editable, and labeled; verify the page tabs are gone (or repurposed per the clarification answer); verify confidence badges still appear where confidence data is available.

**Acceptance Scenarios**:

1. **Given** a consolidated record in the database, **When** the reviewer opens it, **Then** all 14 schema fields render as a single form with the existing UX vocabulary applied.
2. **Given** edits to the consolidated fields, **When** the reviewer saves, **Then** the consolidated record is updated and re-loadable with the new values.

---

### Edge Cases

- A schema field is missing from every page → the field is persisted with `ocrValue = null` (string fields) or `ocrValue = false` (boolean fields), `ocrConfidence = null`, `reviewedValue = null`, and `fieldStatus = "Pending"`; `pageCount` is still accurate. Reviewer sees an empty editable input.
- Same single-value field has conflicting values across pages (e.g., `accusedName` differs on page 1 vs. page 2) → the value from the page with the highest Document Intelligence confidence score for that field is stored as `ocrValue`, and `ocrConfidence` is that same highest score; ties are broken by lowest page number.
- A page returns a single-value field with no confidence score → that page is treated as the lowest-priority candidate (used only if no other page returns the field with a confidence score); when used, `ocrConfidence` is persisted as `null`.
- `mainCharge` or `additionalCharges` is missing from some pages but present on others → concatenation skips the missing pages; ordering remains by page number; the aggregated `ocrConfidence` is the minimum confidence among the contributing pages (pages that contributed nothing are excluded from the minimum).
- Only one page contributes to a concatenated field → the aggregated `ocrConfidence` equals that page's confidence (the minimum of a single value).
- Pages arrive out of order from upstream → consolidation always sorts by page number before concatenation.
- A PDF contains pages from two different logical documents (different `fileTkNumber` values) → two consolidated records are emitted, each with its own `pageCount`.
- A signature field is returned by Document Intelligence with `type ≠ "signature"` (probable model/schema mismatch) → the signature is treated as not captured: `ocrValue = false`, `ocrConfidence = null`, `fieldStatus = "Pending"`; a warning is logged.
- A signature field is returned with `type = "signature"` but `valueSignature ≠ "signed"` (e.g., `"unsigned"` or missing) on every page → `ocrValue = false`, `ocrConfidence = null`, `fieldStatus = "Pending"`; no warning is logged (this is normal "unsigned" output, not a schema mismatch).
- A signature field is returned with `type = "signature"` and `valueSignature = "signed"` on multiple pages → `ocrValue = true`, `ocrConfidence` is the highest per-page `confidence` among those pages.
- A reviewer edits a field and then reverts the edit back to the original `ocrValue` → `fieldStatus` becomes `Confirmed` (not `Corrected`) and `reviewedValue` equals `ocrValue`.
- A reviewer saves a partial review (some fields touched, some not) → only the touched fields move out of `Pending`; the record-level `reviewStatus` remains `Pending` until every field is non-`Pending`.
- Existing per-page records present in Cosmos DB from previous runs → the documents container is wiped during deployment of this feature; only documents processed under the new schema appear afterward.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The processor MUST emit exactly one persisted record per logical document, where logical document boundaries are determined by the configured identifier field (currently `fileTkNumber` per the OCR samples; see Assumptions).
- **FR-002**: Each persisted record MUST contain the following 14 schema fields. Each schema field (except `pageCount`) MUST itself be a structured object carrying the per-field properties defined in **FR-014**. Field types refer to the type of the underlying `ocrValue` / `reviewedValue`:
  - `fileTkNumber` (string)
  - `pageCount` (integer; replaces `pageNumber`; stored as a plain integer, not as a per-field structure — see FR-014)
  - `criminalCodeForm` (string)
  - `policeFileNumber` (string)
  - `agency` (string)
  - `accusedSex` (string)
  - `accusedName` (string)
  - `accusedDatefBirth` (string) *— field name preserved exactly as provided by the user, including the typo, for parity with upstream OCR labels*
  - `mainCharge` (string, concatenated across pages)
  - `signedOn` (string)
  - `judgeSignature` (boolean)
  - `endorsementSignature` (boolean)
  - `endorsementSignedOn` (string)
  - `additionalCharges` (string, concatenated across pages)
- **FR-003**: The persisted record MUST NOT contain a per-page `pageNumber` field; the page count MUST be exposed as `pageCount` only.
- **FR-004**: For single-value string fields, the system MUST select the canonical value as follows: (a) consider only pages where the field is present and non-empty; (b) pick the value from the page with the highest Document Intelligence confidence score for that field; (c) break ties by lowest page number; (d) if no page reports a confidence score, fall back to the first non-empty occurrence in page order.
- **FR-005**: For `mainCharge` and `additionalCharges`, the system MUST concatenate `ocrValue` from all pages where the field is present and non-empty, in ascending page order, separated by a single newline (`\n`). The aggregated `ocrConfidence` for the concatenated field MUST be the **minimum** of the per-page confidence scores among the contributing pages; if no contributing page reports a confidence score, the aggregated `ocrConfidence` MUST be `null`.
- **FR-006**: For `judgeSignature` and `endorsementSignature`, the system MUST treat the field as a signature only when Document Intelligence returns it with `type = "signature"` and `valueSignature = "signed"` on at least one page; in that case the consolidated `ocrValue` MUST be `true` and `ocrConfidence` MUST be the highest `confidence` reported by any page where the field was returned as `"signed"` (ties broken by lowest page number, per FR-004's general rule). If Document Intelligence returns the field with `type` other than `"signature"`, with `valueSignature` other than `"signed"` (e.g., `"unsigned"`, missing, or any other value), or omits the field entirely on every page, the consolidated `ocrValue` MUST be `false` and `ocrConfidence` MUST be `null`. The system MUST log a warning at information level whenever a signature field is encountered with `type` other than `"signature"` (indicating a probable model/schema mismatch), including operation ID, page number, and the actual returned `type`.
- **FR-007**: The system MUST persist the consolidated record to the existing documents container in Cosmos DB, replacing — not augmenting — the previous per-page persistence model for new processing runs.
- **FR-008**: The system MUST preserve the existing `Operation` lifecycle (`Running` → `Succeeded`/`Failed`/`Cancelled`), the existing per-document PDF blob in `processed-documents`, and the existing review-status / assignment fields on each record.
- **FR-009**: When a schema field is absent from every page of a document, the system MUST persist `ocrValue = null` for string fields, `ocrValue = false` for boolean fields, `ocrConfidence = null`, `reviewedValue = null`, and `fieldStatus = "Pending"`. `pageCount` MUST always be a positive integer reflecting the actual number of source pages for the document.
- **FR-010**: As part of deploying this feature, the existing documents container in Cosmos DB MUST be wiped (all per-page legacy records removed); the deployment runbook MUST document this destructive step, and the deployment MUST NOT proceed if the operator has not explicitly acknowledged it. No automated migration of legacy per-page records to the new schema is required or supported.
- **FR-011**: The WebApp Review page MUST render the consolidated schema (one editable form per document) once the persistence change is in effect; the existing per-page tab UI MUST be updated or removed accordingly. Confidence-level UX vocabulary defined in `docs/REVIEW-PAGE-UX.md` MUST continue to apply, sourcing each field's color/badge from the field's `ocrConfidence` value, and MUST visually distinguish per-field statuses (`Pending`, `Confirmed`, `Corrected`).
- **FR-012**: The behavior described in FR-001 through FR-011 and FR-014 through FR-018 MUST be covered by automated tests in the `tests/` project, written before the corresponding production code per the constitution's TDD mandate.
- **FR-013**: The system MUST log, at information level, the consolidation outcome for each document including operation ID, identifier, source page count, and the count of schema fields populated vs. left null/false.
- **FR-014**: Every schema field except `pageCount` MUST be persisted as a structured object with the following properties:
  - `ocrValue` — the value produced by consolidation (string, boolean, or `null`); immutable after extraction.
  - `ocrConfidence` — a number in `[0.0, 1.0]` or `null`; immutable after extraction; for single-value string fields this is the confidence reported by the page selected per FR-004; for concatenated fields this is the minimum confidence per FR-005; for boolean signature fields this is the highest per-page `confidence` among pages where the signature was detected as `"signed"` per FR-006, or `null` if no page reported it as `"signed"`; for any field where Document Intelligence does not report a confidence, this is `null`.
  - `reviewedValue` — the value set by a reviewer; `null` until a reviewer interacts with the field.
  - `reviewedAt` — UTC timestamp of the most recent reviewer interaction with this field; `null` until then.
  - `reviewedBy` — identifier of the reviewer who performed the most recent interaction with this field; `null` until then.
  - `fieldStatus` — one of `Pending`, `Confirmed`, `Corrected`. Defaults to `Pending`.
  `pageCount` is exempt from this structure and is persisted as a plain integer.
- **FR-015**: The system MUST never modify `ocrValue` or `ocrConfidence` after the initial extraction. Reviewer edits MUST only ever modify `reviewedValue`, `reviewedAt`, `reviewedBy`, and `fieldStatus` of the affected field(s) and the record-level `reviewStatus` per FR-017.
- **FR-016**: Per-field state transitions MUST follow these rules:
  - A field's `fieldStatus` becomes `Confirmed` when a reviewer accepts the field with `reviewedValue` equal to `ocrValue`.
  - A field's `fieldStatus` becomes `Corrected` when a reviewer sets `reviewedValue` to a value different from `ocrValue`.
  - If a reviewer subsequently reverts `reviewedValue` to equal `ocrValue`, `fieldStatus` becomes `Confirmed` (not `Pending`).
  - A field's `fieldStatus` MUST NOT transition back to `Pending` once it has left that state.
- **FR-017**: The record-level `reviewStatus` MUST be derived from per-field statuses: `Pending` if any schema field's `fieldStatus` is `Pending`, otherwise `Reviewed`. The record-level `reviewStatus` MUST be recomputed and persisted on every save.
- **FR-018**: The WebApp Review page MUST allow partial reviews: saving with some fields still in `Pending` MUST be permitted; only the fields the reviewer interacted with on that save MUST transition out of `Pending`.

### Key Entities

- **ProcessedDocument**: The new consolidated record. One per logical document. Holds the 14 schema fields plus operational metadata (id, original file name, processed-at timestamp, blob URL, record-level `reviewStatus`, assignment). Replaces the previous shape of `DocumentOcrEntity.ExtractedData` (which held a per-page `Pages[]` array). The same record represents both the unreviewed (`reviewStatus = "Pending"`, every field `Pending`) and the reviewed (`reviewStatus = "Reviewed"`, every field `Confirmed` or `Corrected`) states.
- **SchemaField**: The structured per-field object defined in **FR-014**. Carries `ocrValue`, `ocrConfidence`, `reviewedValue`, `reviewedAt`, `reviewedBy`, `fieldStatus`. Used for every schema field except `pageCount`.
- **PageOcrResult** *(unchanged)*: Internal, transient. Per-page OCR output produced by Document Intelligence. Used as input to consolidation; not persisted.
- **Operation** *(unchanged)*: Tracks the long-running pipeline status surfaced via the Operations API.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After processing any test PDF, the documents container holds exactly one record per logical document (verified by counting records grouped by `fileTkNumber`).
- **SC-002**: Every persisted record contains all 14 schema fields with the correct types and the per-field structure defined in FR-014; no record contains a `pageNumber` field or a `Pages[]` array.
- **SC-003**: For multi-page documents containing `mainCharge` or `additionalCharges` on more than one page, the persisted `ocrValue` contains the contributions from every contributing page in page order with no duplication and no omission, and the persisted `ocrConfidence` equals the minimum of the contributing pages' confidence values (or `null` if none reported one).
- **SC-004**: A reviewer opening any processed document in the WebApp can read and edit all 14 fields in under 30 seconds with no need to switch tabs or navigate per-page views.
- **SC-005**: The full automated test suite passes on the final commit, and every new behavioral requirement (FR-001 through FR-011 and FR-014 through FR-018) maps to at least one test that demonstrably failed prior to its corresponding implementation commit.
- **SC-006**: No regression in pipeline throughput: end-to-end processing time for a typical 10-page PDF remains within the constitution's p95 budget of 60 seconds.
- **SC-007**: For any reviewed record, the original OCR extraction is fully recoverable from the persisted document: for every schema field, `ocrValue` and `ocrConfidence` match the values produced at extraction time, regardless of how many reviewer edits have occurred.
- **SC-008**: For any record, `reviewStatus = "Reviewed"` if and only if every schema field has `fieldStatus ∈ {"Confirmed", "Corrected"}`; this invariant holds after every save (verified by an automated test).

## Assumptions

- The aggregation identifier remains `fileTkNumber` (per the sample OCR data and the existing `DocumentProcessing:IdentifierFieldName` configuration). If a different field is required, it can be changed via configuration without re-specifying the feature.
- Field names in the user's schema are reproduced verbatim, including `accusedDatefBirth` (apparent typo of "DateOfBirth"). Renaming would be a separate, breaking change requiring downstream coordination.
- The default concatenation separator for `mainCharge` and `additionalCharges` is a single newline (`\n`). A different separator (space, semicolon, custom) can be introduced later without changing the persisted shape.
- `reviewedBy` stores the authenticated user principal supplied by the WebApp's existing Microsoft Entra ID integration (e.g., the user's UPN or object ID). The exact identifier shape is a planning-time decision.
- The WebApp's existing record-level review-status / assignment fields (`assignedTo`, `reviewedBy`, `reviewedAt` on the document) are superseded for purposes of per-field tracking by the new `SchemaField` properties; the record-level fields remain for backward-compatible UI labels ("who last touched this record") but MUST be derived from the per-field state on save.
- Existing PDF blobs in the `processed-documents` container remain unchanged; only the database record shape changes.
- The Operations API surface (`Running`/`Succeeded`/`Failed`/`Cancelled`) is unchanged; only the contents of completed-operation results change.
- Existing unit tests in `tests/` continue to pass; only the per-page-persistence assertions (if any) need to be updated alongside the new tests.
- "Major feature, TDD mandatory" is interpreted per Constitution Principle II: every new behavioral requirement gets a failing test first, then implementation. No integration/E2E tests against live Azure are required (and are forbidden by Principle II).

---

## Resolved Clarifications

- **Q1 (Migration policy)** → **Drop / wipe**. The Cosmos DB documents container is wiped during deployment of this feature; only PDFs processed under the new schema appear afterward. No automated migration is required. Encoded in **FR-010** and the corresponding edge case.
- **Q2 (Single-value conflict resolution)** → **Highest-confidence-wins**, ties broken by lowest page number, with a deterministic fallback when confidence scores are absent. Encoded in **FR-004** and the corresponding edge case.
- **Q3 (Signature semantics)** → Document Intelligence returns signatures as `{ type: "signature", valueSignature: "signed" | other, confidence: <float> }`. The consolidated `ocrValue` is `true` only when at least one page reports `type = "signature"` AND `valueSignature = "signed"`; otherwise `false`. `ocrConfidence` is the highest per-page `confidence` among pages where the signature was detected as `"signed"` (or `null` if none). A `type` other than `"signature"` triggers a warning log (probable model/schema mismatch). Encoded in **FR-006**, **FR-014**, and the corresponding edge cases.
- **Q4 (Aggregated confidence for concatenated fields)** → **Minimum** of the contributing pages' confidence values ("chain is as strong as its weakest link"); `null` if no contributing page reports a confidence. Encoded in **FR-005** and **FR-014**.
- **Q5 (Audit trail of original OCR values)** → **Yes, per field forever**: every schema field persists immutable `ocrValue` / `ocrConfidence` alongside mutable `reviewedValue` / `reviewedAt` / `reviewedBy` / `fieldStatus`. One schema, two end-states (unreviewed = all `Pending`; reviewed = all `Confirmed`/`Corrected`). Encoded in **FR-014**, **FR-015**, and **SC-007**.
- **Q6 (Per-field review status & record-level rollup)** → **Per-field `Pending` / `Confirmed` / `Corrected`** with record-level `reviewStatus = Reviewed` only when every field is non-`Pending`; partial reviews are supported. Encoded in **FR-016**, **FR-017**, **FR-018**, and **SC-008**.
