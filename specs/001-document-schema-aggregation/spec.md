# Feature Specification: Consolidated Processed-Document Schema

**Feature Branch**: `001-document-schema-aggregation`
**Created**: 2026-05-01
**Status**: Draft (clarifications Q1–Q11 resolved)
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

### User Story 5 - Reviewer checks out a document, edits, and checks it back in (Priority: P1)

A reviewer takes exclusive ownership of a record by checking it out, makes any number of saves while it is checked out, and releases the lock by checking it in. Other reviewers can see the record (read-only) while it is checked out but cannot edit it. Check-in releases the lock regardless of how many fields the reviewer marked as `Confirmed` / `Corrected`; review status is independent and is derived from per-field state per FR-017.

**Why this priority**: Without an explicit ownership model, two reviewers can edit the same record concurrently and the last save silently overwrites the first. Checkout removes that failure mode, gives reviewers explicit control over which records they own, and keeps the data model simple (no merge logic). P1 because the entire review workflow depends on it.

**Independent Test**: Reviewer A checks out a record. Verify reviewer B sees it as locked and cannot edit. Reviewer A saves twice (partial reviews) and then checks in without marking every field. Verify the record is unlocked, all of A's saved edits are persisted, the record-level `reviewStatus` is still `Pending` (because not every field is non-`Pending`), and `lastCheckedInBy` / `lastCheckedInAt` reflect A's check-in.

**Acceptance Scenarios**:

1. **Given** an unlocked record, **When** reviewer A checks it out, **Then** the record gains `checkedOutBy = A` and `checkedOutAt = <now>`; reviewer B sees the record in the list with a "checked out by A" indicator and cannot open it for editing.
2. **Given** reviewer A holds a checkout, **When** reviewer A clicks Save, **Then** edits to `reviewedValue` / `fieldStatus` are persisted; the lock is **not** released; A can continue editing.
3. **Given** reviewer A holds a checkout and has saved some edits, **When** reviewer A clicks Check In, **Then** any unsaved edits in this session are persisted, the lock is released (`checkedOutBy` and `checkedOutAt` cleared), `lastCheckedInBy = A` and `lastCheckedInAt = <now>` are set, and the record-level `reviewStatus` is recomputed per FR-017.
4. **Given** reviewer A holds a checkout, **When** reviewer A clicks Cancel Checkout, **Then** edits made in the current browser session that were never saved are discarded, edits previously saved during this checkout are kept, the lock is released, `lastCheckedInBy` / `lastCheckedInAt` are **not** updated (Cancel is not a check-in), and the record returns to the pool.
5. **Given** a record was checked out more than 24 hours ago, **When** any reviewer attempts to check it out, **Then** the system auto-releases the stale checkout (preserving previously-saved edits), records the auto-release in logs at warning level with the original `checkedOutBy` and `checkedOutAt`, and grants the new checkout.
6. **Given** a checkout brings every field's `fieldStatus` to `Confirmed` or `Corrected` for the first time, **When** the reviewer checks in, **Then** the record-level `reviewStatus` becomes `Reviewed`, `reviewedBy` is set to the checking-in reviewer, and `reviewedAt` is set to the check-in timestamp; subsequent check-outs and check-ins MUST NOT modify `reviewedBy` or `reviewedAt`.
7. **Given** reviewer A has many records checked out simultaneously (no per-reviewer cap), **When** A checks out an additional record, **Then** the operation succeeds and all prior checkouts remain held by A.

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
- A PDF is re-processed (re-queued, redelivered, or manually re-uploaded) and a record with the same `fileTkNumber` already exists → the new processing run is skipped: the new extraction is logged at warning level with operation ID, identifier, and the existing record's id; nothing is overwritten. The operator MUST explicitly delete the existing record to force re-processing. The operation transitions to `Succeeded` (not `Failed`) with a note that the document was skipped as a duplicate.
- Identifier field (`fileTkNumber`) missing on one or more pages of a multi-document PDF → forward-fill: pages without `fileTkNumber` are attached to the nearest preceding page that has one. Leading pages with no preceding identifier form a synthetic record keyed `unknown-<blob>-<firstPage>`. The consolidated record records, per contributing page, whether the page was attributed by direct extraction or by inference (forward-fill); a warning is logged with operation ID, blob name, and the inferred page numbers. The Review page surfaces a visible indicator on records that contain any inferred pages so the reviewer can verify the document boundary.
- Identifier field missing on every page of the entire PDF → a single synthetic record keyed `unknown-<blob>-1` is emitted with all pages marked as inferred; warning logged.
- Two reviewers attempt to open the same record → the first to click Check Out gains exclusive ownership; the second sees the record in the list with a "checked out by <user>" indicator and a read-only view; no concurrent editing is possible.
- A reviewer attempts to check out a record they already hold → the operation is a no-op (success); the existing checkout is preserved.
- A reviewer attempts to check in a record they do not hold → the operation fails with a clear error; no state is modified.
- A reviewer's checkout has been held for more than 24 hours of wall-clock time without any save or check-in → the next reviewer to attempt check-out triggers an auto-release: previously-saved edits are kept, `checkedOutBy` and `checkedOutAt` are cleared, a warning is logged, and the new checkout proceeds.
- A record is checked out and the holder makes no edits before checking in → check-in succeeds; `lastCheckedInBy` / `lastCheckedInAt` are updated; no per-field state changes; record-level `reviewStatus` is unchanged.
- A reviewer holds checkouts on many records simultaneously → supported with no system-imposed cap; each record's checkout is tracked independently.

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
  - `accusedDateOfBirth` (DateOnly) *— renamed 2026-05-03 to fix the original typo (`accusedDatefBirth`); the upstream Document Intelligence model emits this field as `accusedDateOfBirth` so no mapper alias is required. Persisted as ISO `yyyy-MM-dd` string when the OCR text matches one of the supported patterns, otherwise `null` with the raw OCR text preserved in the sibling `ocrRawText` property (FR-002a).*
  - `mainCharge` (string, concatenated across pages)
  - `signedOn` (DateOnly — see FR-002a)
  - `judgeSignature` (boolean)
  - `endorsementSignature` (boolean)
  - `endorsementSignedOn` (DateOnly — see FR-002a)
  - `additionalCharges` (string, concatenated across pages)
- **FR-002a**: For the three date fields (`accusedDateOfBirth`, `signedOn`, `endorsementSignedOn`), the page-level OCR text MUST be parsed by a deterministic two-pattern parser:

  1. `^\s*(?<YEAR>\d{4})(?<MON>JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)(?<DAY>\d{1,2})\s*$` (case-insensitive)
  2. `^\s*(?<DAY>\d{1,2})\s*(?:ST|ND|RD|TH)?\s*DAY\s*OF\s*(?<MONTH>JANUARY|...|DECEMBER)\s*,?\s*(?<YEAR>\d{4})\s*$` (case-insensitive)

  When the highest-confidence page's text matches either pattern AND the resulting year/month/day is a valid calendar date, the persisted `ocrValue` MUST be the ISO `yyyy-MM-dd` string and the original raw OCR text MUST be persisted to `ocrRawText`. When the text fails to parse OR yields a calendar-invalid date, the persisted `ocrValue` MUST be `null`, the raw OCR text MUST still be persisted to `ocrRawText`, and the system MUST log a warning at information level. Reviewer-supplied date corrections MUST be valid ISO `yyyy-MM-dd` strings AND MUST NOT be in the future (UTC `today` inclusive); violations MUST be rejected before persistence. The Review UI MUST render an HTML `<input type="date">` (with a client-side `max=today`) for these fields, with the raw OCR text shown as the OCR display when parsing failed.

- **FR-003**: The persisted record MUST NOT contain a per-page `pageNumber` field; the page count MUST be exposed as `pageCount` only.
- **FR-004**: For single-value string fields, the system MUST select the canonical value as follows: (a) consider only pages where the field is present and non-empty; (b) pick the value from the page with the highest Document Intelligence confidence score for that field; (c) break ties by lowest page number; (d) if no page reports a confidence score, fall back to the first non-empty occurrence in page order.
- **FR-005**: For `mainCharge` and `additionalCharges`, the system MUST concatenate `ocrValue` from all pages where the field is present and non-empty, in ascending page order, separated by a single newline (`\n`). The aggregated `ocrConfidence` for the concatenated field MUST be the **minimum** of the per-page confidence scores among the contributing pages; if no contributing page reports a confidence score, the aggregated `ocrConfidence` MUST be `null`.
- **FR-006**: For `judgeSignature` and `endorsementSignature`, the system MUST treat the field as a signature only when Document Intelligence returns it with `type = "signature"` and `valueSignature = "signed"` on at least one page; in that case the consolidated `ocrValue` MUST be `true` and `ocrConfidence` MUST be the highest `confidence` reported by any page where the field was returned as `"signed"` (ties broken by lowest page number, per FR-004's general rule). If Document Intelligence returns the field with `type` other than `"signature"`, with `valueSignature` other than `"signed"` (e.g., `"unsigned"`, missing, or any other value), or omits the field entirely on every page, the consolidated `ocrValue` MUST be `false` and `ocrConfidence` MUST be `null`. The system MUST log a warning at information level whenever a signature field is encountered with `type` other than `"signature"` (indicating a probable model/schema mismatch), including operation ID, page number, and the actual returned `type`.
- **FR-007**: The system MUST persist the consolidated record to the existing documents container in Cosmos DB, replacing — not augmenting — the previous per-page persistence model for new processing runs.
- **FR-008**: The system MUST preserve the existing `Operation` lifecycle (`Running` → `Succeeded`/`Failed`/`Cancelled`), the existing per-document PDF blob in `processed-documents`, and the existing review-status / assignment fields on each record.
- **FR-009**: When a schema field is absent from every page of a document, the system MUST persist `ocrValue = null` for string fields, `ocrValue = false` for boolean fields, `ocrConfidence = null`, `reviewedValue = null`, and `fieldStatus = "Pending"`. `pageCount` MUST always be a positive integer reflecting the actual number of source pages for the document.
- **FR-010**: As part of deploying this feature, the existing documents container in Cosmos DB MUST be wiped (all per-page legacy records removed); the deployment runbook MUST document this destructive step, and the deployment MUST NOT proceed if the operator has not explicitly acknowledged it. No automated migration of legacy per-page records to the new schema is required or supported.
- **FR-011**: The WebApp Review page MUST render the consolidated schema (one editable form per document) once the persistence change is in effect; the existing per-page tab UI MUST be updated or removed accordingly. Confidence-level UX vocabulary defined in `docs/REVIEW-PAGE-UX.md` MUST continue to apply, sourcing each field's color/badge from the field's `ocrConfidence` value, and MUST visually distinguish per-field statuses (`Pending`, `Confirmed`, `Corrected`).
- **FR-012**: The behavior described in FR-001 through FR-011 and FR-014 through FR-025 MUST be covered by automated tests in the `tests/` project, written before the corresponding production code per the constitution's TDD mandate.
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
- **FR-019**: The processor MUST treat re-processing of an existing record as a no-op: when a consolidated record with the same `fileTkNumber` already exists in the documents container, the new extraction MUST NOT be persisted, MUST NOT modify any field of the existing record, and MUST be logged at warning level with operation ID, identifier, existing record id, and a `"duplicate skipped"` reason. The owning operation MUST transition to `Succeeded` with a status detail noting the skip. Forcing re-processing requires an operator to delete the existing record first; no automated override is provided.
- **FR-020**: The processor MUST forward-fill the aggregation identifier (`fileTkNumber`) across pages: any page that does not directly extract `fileTkNumber` MUST be attributed to the nearest preceding page that did. Leading pages with no preceding identifier MUST form a synthetic record keyed `unknown-<blob>-<firstPage>`. The consolidated record MUST record per contributing page whether the page was attributed by direct extraction (`extracted`) or by inference (`inferred`), via a `pageProvenance` collection (exact storage shape is a planning-time decision). The system MUST log a warning at information level whenever any pages are attributed by inference, including operation ID, blob name, target identifier, and the inferred page numbers.
- **FR-021**: The system MUST support reviewer checkout / check-in of consolidated records:
  - Each record MUST carry `checkedOutBy` (string identifier of the holder, or `null`) and `checkedOutAt` (UTC timestamp, or `null`).
  - A checkout request MUST atomically set both fields when they are currently `null`; if `checkedOutBy` is non-null and refers to another reviewer, the request MUST fail with a clear conflict error.
  - A checkout request from the current holder MUST be a successful no-op.
  - While a record is checked out, only the holder MUST be permitted to modify any field. Other reviewers MAY view the record read-only.
  - A check-in request from the holder MUST persist any pending edits, clear `checkedOutBy` and `checkedOutAt`, set `lastCheckedInBy` and `lastCheckedInAt`, and recompute the record-level `reviewStatus` per FR-017.
  - A check-in request from a non-holder MUST fail with a clear error and modify no state.
  - A Cancel Checkout request from the holder MUST discard edits made in the current browser session that were never saved, MUST keep edits previously saved during the same checkout, MUST clear `checkedOutBy` and `checkedOutAt`, and MUST NOT update `lastCheckedInBy` / `lastCheckedInAt` (Cancel is not a check-in).
  - The system MUST NOT impose a per-reviewer cap on the number of simultaneous checkouts.
- **FR-022**: A checkout that has been held for more than 24 hours of wall-clock time without any save or check-in is considered stale. The system MUST auto-release stale checkouts opportunistically: when any reviewer attempts to check out a stale record, the system MUST clear `checkedOutBy` and `checkedOutAt`, preserve all previously-saved edits, log a warning at information level with the original holder and timestamps, and proceed with the requesting reviewer's checkout.
- **FR-023**: Saves during a checkout MUST be explicit (driven by an explicit Save action), MAY occur any number of times during a single checkout, and MUST persist `reviewedValue` / `reviewedAt` / `reviewedBy` / `fieldStatus` changes for the touched fields. Save MUST NOT release the checkout. Continuous autosave is out of scope.
- **FR-024**: The record-level lifecycle fields MUST have the following distinct semantics, kept independent of one another:
  - `lastCheckedInBy` / `lastCheckedInAt` — set on every check-in (including check-ins where no field transitioned out of `Pending`); represent "who most recently returned this record to the pool, and when". Cancel Checkout MUST NOT update these.
  - `reviewedBy` / `reviewedAt` — set the first time the record-level `reviewStatus` transitions from `Pending` to `Reviewed`; represent "who first brought this record to fully-reviewed, and when". Once set, they MUST NOT be modified by any subsequent checkout, save, or check-in.
- **FR-025**: The documents list page MUST display, for each record, at minimum: the record-level `reviewStatus` (`Pending` / `Reviewed`), a fields-reviewed progress indicator showing the count of non-`Pending` fields out of the total number of schema fields with per-field state (e.g., `5/13`; `pageCount` is excluded because it has no `fieldStatus`), and the current `checkedOutBy` value (or an empty indicator when `null`). The list MUST be filterable by `reviewStatus` and by checkout state (any / mine / others / unlocked).

### Key Entities

- **ProcessedDocument**: The new consolidated record. One per logical document. Holds the 14 schema fields plus operational metadata (id, original file name, processed-at timestamp, blob URL, record-level `reviewStatus`, `reviewedBy`, `reviewedAt`, `lastCheckedInBy`, `lastCheckedInAt`, `checkedOutBy`, `checkedOutAt`, `pageProvenance`). Replaces the previous shape of `DocumentOcrEntity.ExtractedData` (which held a per-page `Pages[]` array). The same record represents both the unreviewed (`reviewStatus = "Pending"`, every field `Pending`) and the reviewed (`reviewStatus = "Reviewed"`, every field `Confirmed` or `Corrected`) states.
- **SchemaField**: The structured per-field object defined in **FR-014**. Carries `ocrValue`, `ocrConfidence`, `reviewedValue`, `reviewedAt`, `reviewedBy`, `fieldStatus`. Used for every schema field except `pageCount`.
- **PageProvenance**: A per-record collection (one entry per contributing source page) that records whether the page's identifier (`fileTkNumber`) was attributed by direct extraction (`extracted`) or by forward-fill inference (`inferred`). Used to surface document-boundary uncertainty to the reviewer per FR-020.
- **PageOcrResult** *(unchanged)*: Internal, transient. Per-page OCR output produced by Document Intelligence. Used as input to consolidation; not persisted.
- **Operation** *(unchanged)*: Tracks the long-running pipeline status surfaced via the Operations API.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After processing any test PDF, the documents container holds exactly one record per logical document (verified by counting records grouped by `fileTkNumber`).
- **SC-002**: Every persisted record contains all 14 schema fields with the correct types and the per-field structure defined in FR-014; no record contains a `pageNumber` field or a `Pages[]` array.
- **SC-003**: For multi-page documents containing `mainCharge` or `additionalCharges` on more than one page, the persisted `ocrValue` contains the contributions from every contributing page in page order with no duplication and no omission, and the persisted `ocrConfidence` equals the minimum of the contributing pages' confidence values (or `null` if none reported one).
- **SC-004**: A reviewer opening any processed document in the WebApp can read and edit all 14 fields in under 30 seconds with no need to switch tabs or navigate per-page views.
- **SC-005**: The full automated test suite passes on the final commit, and every new behavioral requirement (FR-001 through FR-011 and FR-014 through FR-025) maps to at least one test that demonstrably failed prior to its corresponding implementation commit.
- **SC-006**: No regression in pipeline throughput: end-to-end processing time for a typical 10-page PDF remains within the constitution's p95 budget of 60 seconds.
- **SC-007**: For any reviewed record, the original OCR extraction is fully recoverable from the persisted document: for every schema field, `ocrValue` and `ocrConfidence` match the values produced at extraction time, regardless of how many reviewer edits have occurred.
- **SC-008**: For any record, `reviewStatus = "Reviewed"` if and only if every schema field has `fieldStatus ∈ {"Confirmed", "Corrected"}`; this invariant holds after every save (verified by an automated test).
- **SC-009**: Re-processing a PDF whose `fileTkNumber` already has a record in the documents container produces no change to that record (verified by snapshot equality before/after) and a warning log entry; the owning operation completes as `Succeeded`.
- **SC-010**: For any persisted record, the `pageProvenance` collection length equals `pageCount`, and every entry is one of `extracted` / `inferred`; records containing any `inferred` entry are visually flagged on the Review page.
- **SC-011**: At any point in time, every record is held by at most one reviewer (verified by a uniqueness assertion on `checkedOutBy` per record); a checkout request against a held record fails with a clear conflict error and never silently succeeds.
- **SC-012**: After a check-in, `lastCheckedInBy` and `lastCheckedInAt` reflect the checking-in reviewer and the check-in timestamp. After the first check-in that brings the record to fully-reviewed, `reviewedBy` and `reviewedAt` are also set; subsequent activity updates only `lastCheckedInBy` / `lastCheckedInAt` and never modifies `reviewedBy` / `reviewedAt` (verified by an automated test).

## Assumptions

- The aggregation identifier remains `fileTkNumber` (per the sample OCR data and the existing `DocumentProcessing:IdentifierFieldName` configuration). If a different field is required, it can be changed via configuration without re-specifying the feature.
- Field names in the user's schema are reproduced verbatim with one fix: `accusedDatefBirth` was the user's typo and was corrected to `accusedDateOfBirth` on 2026-05-03 after confirming the upstream Document Intelligence model emits the corrected name. The three date fields (`accusedDateOfBirth`, `signedOn`, `endorsementSignedOn`) were retyped from `string` to `DateOnly` at the same time.
- The default concatenation separator for `mainCharge` and `additionalCharges` is a single newline (`\n`). A different separator (space, semicolon, custom) can be introduced later without changing the persisted shape.
- `reviewedBy` stores the authenticated user principal supplied by the WebApp's existing Microsoft Entra ID integration (e.g., the user's UPN or object ID). The exact identifier shape is a planning-time decision.
- The WebApp's existing record-level review-status / assignment fields (`assignedTo`, `reviewedBy`, `reviewedAt` on the document) are superseded for purposes of per-field tracking by the new `SchemaField` properties; the record-level fields remain for backward-compatible UI labels ("who last touched this record") but MUST be derived from the per-field state on save.
- Existing PDF blobs in the `processed-documents` container remain unchanged; only the database record shape changes.
- The Operations API surface (`Running`/`Succeeded`/`Failed`/`Cancelled`) is unchanged; only the contents of completed-operation results change.
- Existing unit tests in `tests/` continue to pass; only the per-page-persistence assertions (if any) need to be updated alongside the new tests.
- "Major feature, TDD mandatory" is interpreted per Constitution Principle II: every new behavioral requirement gets a failing test first, then implementation. No integration/E2E tests against live Azure are required (and are forbidden by Principle II).
- Stale-checkout auto-release is opportunistic (triggered by the next checkout attempt), not driven by a background timer. This avoids introducing a scheduled job for a POC; if reviewer experience demands proactive release in a future iteration, a TimerTrigger function can be added without changing the persisted shape.
- The 24-hour stale threshold is a configurable system constant; changing it does not require a spec amendment.
- Within a single checkout, "saved edits" means edits persisted via an explicit Save action. Cancel Checkout discards only edits made in the current browser session that were never persisted (typed-but-not-saved); edits saved earlier in the same checkout remain.

---

## Resolved Clarifications

- **Q1 (Migration policy)** → **Drop / wipe**. The Cosmos DB documents container is wiped during deployment of this feature; only PDFs processed under the new schema appear afterward. No automated migration is required. Encoded in **FR-010** and the corresponding edge case.
- **Q2 (Single-value conflict resolution)** → **Highest-confidence-wins**, ties broken by lowest page number, with a deterministic fallback when confidence scores are absent. Encoded in **FR-004** and the corresponding edge case.
- **Q3 (Signature semantics)** → Document Intelligence returns signatures as `{ type: "signature", valueSignature: "signed" | other, confidence: <float> }`. The consolidated `ocrValue` is `true` only when at least one page reports `type = "signature"` AND `valueSignature = "signed"`; otherwise `false`. `ocrConfidence` is the highest per-page `confidence` among pages where the signature was detected as `"signed"` (or `null` if none). A `type` other than `"signature"` triggers a warning log (probable model/schema mismatch). Encoded in **FR-006**, **FR-014**, and the corresponding edge cases.
- **Q4 (Aggregated confidence for concatenated fields)** → **Minimum** of the contributing pages' confidence values ("chain is as strong as its weakest link"); `null` if no contributing page reports a confidence. Encoded in **FR-005** and **FR-014**.
- **Q5 (Audit trail of original OCR values)** → **Yes, per field forever**: every schema field persists immutable `ocrValue` / `ocrConfidence` alongside mutable `reviewedValue` / `reviewedAt` / `reviewedBy` / `fieldStatus`. One schema, two end-states (unreviewed = all `Pending`; reviewed = all `Confirmed`/`Corrected`). Encoded in **FR-014**, **FR-015**, and **SC-007**.
- **Q6 (Per-field review status & record-level rollup)** → **Per-field `Pending` / `Confirmed` / `Corrected`** with record-level `reviewStatus = Reviewed` only when every field is non-`Pending`; partial reviews are supported. Encoded in **FR-016**, **FR-017**, **FR-018**, and **SC-008**.
- **Q7 (Re-processing existing records)** → **Skip**. If a record with the same `fileTkNumber` already exists, the new extraction is logged and discarded; nothing is overwritten. Operator must delete the existing record to force re-processing. Encoded in **FR-019**, **SC-009**, and the corresponding edge case.
- **Q8 (Missing identifier field on some pages)** → **Forward-fill with provenance + flag**. Pages without `fileTkNumber` attach to the nearest preceding page that has one; leading orphan pages form a synthetic `unknown-<blob>-<firstPage>` record. Per-page provenance (`extracted` vs. `inferred`) is persisted; warning is logged; Review page flags inferred pages so reviewers can verify document boundaries. Encoded in **FR-020**, the **PageProvenance** entity, **SC-010**, and the corresponding edge cases.
- **Q9 (Concurrent reviewer edits)** → **Explicit checkout / check-in** (pessimistic lock by user action). Single holder per record; no per-reviewer cap on concurrent checkouts; check-in is independent of review status. Encoded in **FR-021**, **SC-011**, and the corresponding edge cases. Sub-clarifications:
  - **Q9a (Stale checkouts)** → 24-hour opportunistic auto-release, triggered by the next checkout attempt (no background timer). Encoded in **FR-022**.
  - **Q9b (Saves during checkout)** → Explicit Save button; multiple saves per checkout allowed; check-in performs a final save. No continuous autosave. Encoded in **FR-023**.
  - **Q9c (Cancel Checkout)** → Discards only in-session unsaved edits; previously-saved edits during the same checkout are kept; lock is released; `lastCheckedInBy` / `lastCheckedInAt` are NOT updated (Cancel is not a check-in). Encoded in **FR-021** and the corresponding edge case.
  - **Q9d (Concurrent checkouts per reviewer)** → No system-imposed cap. Encoded in **FR-021**.
- **Q10 (Record-level `reviewedBy` / `reviewedAt` vs. `lastCheckedInBy` / `lastCheckedInAt`)** → **Keep both, distinct meanings**. `lastCheckedInBy` / `lastCheckedInAt` track the most recent check-in (any check-in). `reviewedBy` / `reviewedAt` track the first transition to fully-reviewed and are immutable thereafter. Encoded in **FR-024** and **SC-012**.
- **Q11 (Documents-list visibility)** → **Minimal + progress**. Columns: `reviewStatus` + `Fields reviewed` (e.g., `5/13`) + `Checked out by`; filterable by review status and checkout state. Encoded in **FR-025**.
