# Feature Specification: Upload Page Range Selection

**Feature Branch**: `002-upload-page-range-selection`  
**Created**: 2026-05-01  
**Status**: Draft  
**Input**: User description: "When the user uploads a PDF, the upload page must allow the user to choose which pages to process. Some uploaded PDFs contain a preamble that must be ignored and not processed. The user must be able to see the uploaded PDF and define the page ranges (just like when printing a document) that must be extracted. The selected ranges are sent with the request so the processor can use that information."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Preview a PDF and select page ranges to process (Priority: P1)

A user uploading a PDF that contains non-relevant front matter (cover page, table of contents, marketing preamble) needs to skip those pages so that only the meaningful content is sent for OCR extraction. The user picks a PDF on the upload page, sees a preview of the document, types a page-range expression in print-dialog style (for example `3-12, 15, 20-25`), and submits. Only the chosen pages are sent for processing.

**Why this priority**: This is the core value of the feature — without it, users either waste OCR cost and time on irrelevant pages or get noisy/incorrect aggregated results when preamble pages produce spurious identifier matches. It is also the user's stated motivation for the request.

**Independent Test**: Can be fully tested by uploading a single multi-page PDF, previewing it, entering a valid range that excludes the first N pages, submitting, and verifying that the resulting operation processed only the selected pages and that the preamble content is absent from the extracted documents.

**Acceptance Scenarios**:

1. **Given** the upload page with a single PDF selected, **When** the user opens the preview, **Then** they can navigate through every page of the PDF and see the total page count displayed.
2. **Given** a previewed PDF with 20 pages, **When** the user enters `3-12, 15` and submits, **Then** the started operation processes exactly pages 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, and 15 (11 pages) and no others.
3. **Given** a previewed PDF, **When** the user leaves the range field empty (or types only whitespace), **Then** the default selection is "all pages" and behavior matches the current upload flow (no regression for users who don't need filtering).
4. **Given** a range field, **When** the user enters non-empty malformed text (e.g. `abc`, `1-`, `5-3`), **Then** submission is blocked with a clear validation message and no upload/operation is started.

---

### User Story 2 - Validate the range expression before upload (Priority: P1)

A user typing a range expression needs immediate, understandable feedback when the expression is malformed or refers to pages outside the PDF, so they can fix it before paying the upload+OCR cost.

**Why this priority**: Without inline validation, invalid input either silently degrades to "process nothing" or causes server-side failures that waste an upload round-trip. Validation is mandatory for a usable form and is small relative to story 1.

**Independent Test**: Can be tested by entering each invalid form (`abc`, `0`, `5-3`, `100-200` on a 20-page PDF, `,,,`, `1-`) and confirming the submit button is disabled or an inline error is shown describing the problem.

**Acceptance Scenarios**:

1. **Given** a 20-page PDF, **When** the user enters `25-30`, **Then** an inline error indicates the range is outside the document's 20 pages and submission is blocked.
2. **Given** any PDF, **When** the user enters `abc` or `1-`, **Then** an inline error indicates the syntax is invalid and submission is blocked.
3. **Given** a valid expression, **When** the user finishes typing, **Then** the UI shows a normalized summary such as "11 pages selected: 3–12, 15".
4. **Given** an expression with overlapping or duplicate pages such as `3-7, 5-10`, **When** validated, **Then** the system accepts it, deduplicates internally, and the summary shows the unique page count (8 pages: 3–10).

---

### User Story 3 - Page-range information is preserved end-to-end (Priority: P2)

A reviewer auditing an extracted document needs to know which pages of the original PDF were processed, so that page references in the extracted results are unambiguous and operations can be reproduced or re-run.

**Why this priority**: This makes the feature trustworthy and auditable. It is P2 because the core extraction value (story 1) works without it, but operational/audit confidence requires it.

**Independent Test**: Submit an upload with a non-default range (e.g., `5-10`), open the resulting operation in the Operations page and an extracted document in the Review page, and verify the originally selected range is displayed on the operation, and that page citations within an extracted document reference that document's own (1..N) page numbering.

**Acceptance Scenarios**:

1. **Given** an operation started with range `5-10`, **When** the user opens the Operations detail view, **Then** the selected page range `5-10` is visible alongside the other operation metadata.
2. **Given** an extracted document that aggregated 3 pages from the processed subset, **When** a reviewer opens the Review page, **Then** per-page provenance and any page citations within that document are numbered 1, 2, 3 (the OCR-extracted page indices belonging to that document), independent of where those pages sat in the original uploaded PDF.
3. **Given** an operation started without specifying a range, **When** the user views the operation, **Then** the page range is shown as "All pages" (or equivalent) for clarity.

---

### Edge Cases

- **Empty PDF / 1-page PDF**: A 1-page PDF must be accepted with default range `1`; the range UI must not require multi-page input.
- **Whitespace and separators**: Inputs like `3 - 12 ,  15` must be tolerated and normalized (trim whitespace, accept `,` separators).
- **Reversed bounds**: `12-3` is rejected with a clear message ("start page must be ≤ end page").
- **Zero or negative pages**: `0`, `-1` rejected; pages are 1-indexed to match print dialogs.
- **All pages excluded**: An expression that somehow resolves to zero pages is defensively rejected; in practice the grammar makes this unreachable.
- **Very large selections**: Selecting all pages of a large PDF must behave identically to the current "no filtering" upload (no new size limits introduced by this feature).
- **Multi-file uploads**: When more than one file is selected, the page-range UI behavior is defined in FR-010.
- **Preview unavailable**: If the browser cannot render the PDF preview (e.g., very large file, browser without PDF support), the user can still type and submit a range expression; validation against page count still occurs.
- **Corrupt/encrypted PDF**: A PDF that cannot be parsed for page count is rejected at upload time with a clear message; no operation is started.
- **Re-running an operation**: When the page range is preserved on the operation record, the user can identify or re-run with the same scope (re-run UX itself is out of scope for this feature, but the data must be present).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The upload page MUST display an interactive preview of each selected PDF, allowing the user to see all pages and the total page count before submitting.
- **FR-002**: The upload page MUST provide a page-range input field per PDF, accepting a print-dialog–style expression composed of 1-indexed page numbers and inclusive ranges separated by commas (examples: `5`, `3-12`, `3-12, 15, 20-25`).
- **FR-003**: The page-range input MUST default to "all pages" when the user does not modify it, preserving the current upload behavior for users who do not need filtering.
- **FR-004**: The system MUST validate the range expression in the browser before submission, rejecting:
  - Syntactically invalid expressions
  - Pages `< 1` or pages greater than the PDF's total page count
  - Reversed bounds (start > end)
  - (An empty or whitespace-only value is **not** an error — it is the implicit "all pages" default per FR-003.)
- **FR-005**: When a valid expression is entered, the UI MUST display a normalized human-readable summary including the total number of unique pages selected (e.g., "11 pages selected: 3–12, 15").
- **FR-006**: The system MUST silently deduplicate overlapping or repeated pages within the expression and process each selected page at most once.
- **FR-007**: Submission MUST be blocked (the upload button disabled or the request rejected) whenever any selected file has an invalid page-range expression.
- **FR-008**: The selected page range MUST be transmitted with the operation start request so that the processor receives it together with the blob reference.
- **FR-009**: The processor MUST extract OCR data only from the pages indicated by the supplied range; pages outside the range MUST NOT be sent to the OCR service. Aggregation, output PDF generation, and persistence MUST operate on the selected subset only.
- **FR-010**: When multiple files are uploaded in one batch, the page-range field MUST be **per-file** so that each PDF can have its own range. A convenience control MUST be available to apply the same expression to all files (e.g., "Apply to all").
- **FR-011**: Page citations within an extracted document (per-page provenance, page references, display labels inside that document) MUST reference the page numbers of that document as produced by OCR — numbered 1..N within the document itself — not the original uploaded PDF's page numbers. The mapping back to original PDF page numbers is captured at the operation level (FR-012) for audit, not embedded in each document's citations.
- **FR-012**: The selected page range MUST be persisted on the operation record so that it can be displayed in the Operations page and used for audit. When no range was specified, the operation MUST display "All pages" or equivalent.
- **FR-013**: The Review page MUST display the original page range that was processed for the document, alongside existing metadata.
- **FR-014**: Existing operations that predate this feature (and any future request that omits a range) MUST be treated as "all pages" — the feature MUST be backward-compatible with the existing queue message contract and existing stored operations.
- **FR-015**: If a PDF cannot be parsed for total page count (corrupt/encrypted), the system MUST reject the file at upload time with a clear error and MUST NOT start an operation for it.

### Key Entities *(include if feature involves data)*

- **Page Range Selection**: A user-supplied, per-file specification of which 1-indexed pages of an uploaded PDF should be processed. Conceptually a set of unique page numbers derived from a print-style expression. Attributes: original expression as typed, normalized list of unique page numbers, total selected count, the source PDF's total page count.
- **Operation (extension)**: The existing per-file processing operation gains an associated Page Range Selection (or the implicit "all pages" value when none was supplied). All downstream artifacts (extracted documents, provenance, aggregated outputs) cite their pages using document-local 1..N numbering (FR-011); the original PDF page-range expression is captured at the operation level only, for audit.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For PDFs that contain unwanted preamble pages, users can exclude those pages and complete the upload in under 60 seconds from file selection to submission, including reviewing the preview.
- **SC-002**: 100% of operations started through the upload page record the page range that was used (explicit list, or the explicit value "all pages"), so any extracted document is traceable to a specific subset of the original PDF.
- **SC-003**: When a user restricts a 50-page PDF to a 10-page subset, the OCR processing cost (pages sent to the OCR service) is reduced by 80% compared to processing the full document, with no extracted-content regression on the selected pages.
- **SC-004**: Invalid range expressions are caught before upload in 100% of cases listed in FR-004; no upload bandwidth or OCR cost is consumed by an invalid range.
- **SC-005**: Users uploading without modifying the range field experience no measurable change in upload time or success rate compared to before this feature (zero-regression default path).
- **SC-006**: Page citations inside an extracted document are consistent with that document's own page count in 100% of audited documents (i.e., a document with N pages cites pages 1..N), regardless of which subset of the original PDF was processed.

## Assumptions

- Users perform PDF uploads from a modern desktop browser capable of rendering an embedded PDF preview; mobile-specific preview optimizations are out of scope.
- The existing per-file size limit (50 MB) and per-batch file count (10) remain unchanged by this feature.
- Page-range syntax follows familiar print-dialog conventions: 1-indexed page numbers, inclusive ranges with `-`, multiple groups separated by `,`. Whitespace around tokens is tolerated. Wildcards (`*`, `all`, "to end") are out of scope unless trivially derivable.
- The processor already iterates pages from a downloaded PDF; restricting iteration to a supplied subset is a localized change rather than a redesign of the OCR pipeline.
- The Operations API and queue message contract can be extended with an optional page-range field while remaining backward-compatible with messages that omit it.
- "Re-run an operation with the same range" is a desirable follow-up but is not part of this feature; only the storage of the range is required.
- The PDF preview component does not need to support annotation, text selection, or thumbnail reordering — read-only paging through the document is sufficient.
