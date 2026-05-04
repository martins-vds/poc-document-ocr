# Review Page UX

> **Status:** current state of the Review page after feature
> `001-document-schema-aggregation`. Earlier per-page tabbed and
> confidence-only layouts are no longer used and have been removed
> from this document.

The Review page (`/review/{identifier}`,
[`Review.razor`](../src/DocumentOcr.WebApp/Components/Pages/Review.razor))
is a single schema-driven form: one row per reviewable field as defined
by [`ProcessedDocumentSchema.FieldNames`](../src/DocumentOcr.Common/Models/ProcessedDocumentSchema.cs).
Cosmos stores exactly one consolidated `SchemaField` per field per
identifier, so there are no per-page tabs.

## Page anatomy

```
┌──────────────────────────────────────────────────────────────────────┐
│  ← Back to Documents     Identifier: TK-2024-001     [ Reviewed ✓ ]  │
│  Original: invoice-batch.pdf · Pages 2,3,5 (Page range: 3-12, 15)    │
├──────────────────────────────────┬───────────────────────────────────┤
│                                  │  ⚠ Pages 4, 5 had their           │
│       PDF viewer                 │     identifier inferred           │
│       (in-page pdf.js)           │  ─────────────────────────────    │
│       deep-links to Page X       │  fileTkNumber       [Pending]    │
│                                  │  OCR: TK-2024-001  conf 0.97     │
│                                  │  [ Reviewed value ___________ ]  │
│                                  │  [Confirm] [Correct]             │
│                                  │  ─────────────────────────────    │
│                                  │  signedOn           [Corrected]  │
│                                  │  OCR: 2024-01-15   conf 0.62     │
│                                  │  Raw text: "Jan 15 2024"         │
│                                  │  [ 2024-01-16 ]                  │
│                                  │  ─────────────────────────────    │
│                                  │  judgeSignature     [Confirmed]  │
│                                  │  OCR: true         conf 0.91     │
│                                  │  ☑ Signed                         │
│                                  │  ─────────────────────────────    │
│                                  │  ... (every catalog field)       │
│                                  │  [ Save & Check In ]             │
└──────────────────────────────────┴───────────────────────────────────┘
```

### Header metadata

- `Identifier` — partition key of the Cosmos record.
- `Page count`, `Page numbers` — pages aggregated into this document.
- `Page range` — mirrors the operation's `pageRange`. Shows `All pages`
  for operations created before feature 002 or with a `null` selection
  (back-compat).
- `Reviewed` / `Pending` badge — record-level [`ReviewStatus`](../src/DocumentOcr.Common/Models/ReviewStatus.cs).

### Checkout banner (FR-021 / FR-025)

| Lock state               | UI                                                             |
| ------------------------ | -------------------------------------------------------------- |
| You hold the checkout    | Info banner; form inputs enabled.                              |
| Held by another reviewer | Warning banner with holder UPN + timestamp; form is read-only. |
| Free                     | `[Check out for review]` button replaces the banner.           |

[`DocumentLockService`](../src/DocumentOcr.Common/Services/DocumentLockService.cs)
opportunistically releases checkouts older than 24 h.

### Page-boundary warning (FR-020)

If any entry in `PageProvenance` has `IdentifierSource == Inferred`, a
yellow banner above the form lists the page numbers whose identifier
was forward-filled. This mirrors the
`FR-020 inferred-identifier pages` warning the processor emits.

## Per-field row

| Element              | Source                               | Notes                                                                                               |
| -------------------- | ------------------------------------ | --------------------------------------------------------------------------------------------------- |
| Field name           | `ProcessedDocumentSchema.FieldNames` | Catalog order.                                                                                      |
| `OCR value`          | `SchemaField.OcrValue`               | Immutable.                                                                                          |
| `Raw text`           | `SchemaField.OcrRawText`             | Only emitted for date fields where parsing produced a normalized `OcrValue`.                        |
| Confidence           | `SchemaField.OcrConfidence`          | Two decimals.                                                                                       |
| Status badge         | `SchemaField.FieldStatus`            | See palette below.                                                                                  |
| Reviewed value input | `SchemaField.ReviewedValue`          | Date input for `IsDateField`, checkbox for `bool`, textarea for `MultiValueFields`, otherwise text. |
| Actions              | —                                    | `[Confirm]` / `[Correct]` accumulate in a pending-edits buffer; `[Save & Check In]` flushes them.   |

### Status palette

| Status      | Bootstrap class | Meaning                                                  |
| ----------- | --------------- | -------------------------------------------------------- |
| `Pending`   | `bg-warning`    | Reviewer has not touched the field.                      |
| `Confirmed` | `bg-success`    | Reviewer accepted `OcrValue` (no value change recorded). |
| `Corrected` | `bg-info`       | Reviewer overrode `OcrValue` with `ReviewedValue`.       |

Confidence color hints (badge tinting) follow the standard scale:
`≥0.90` green, `0.70–0.89` blue, `0.50–0.69` yellow, `<0.50` red.

## Document-level transition

When **every** field is non-`Pending`, the per-row save flips the
record-level `ReviewStatus` to `Reviewed` and stamps the first
reviewer's UPN + timestamp immutably (FR-017 / FR-018). Subsequent
edits update `LastCheckedInBy` / `LastCheckedInAt` but never overwrite
the original reviewer.

## Customizing what shows up here

The Review form is **schema-driven**. To add, remove, or re-order
fields, edit `ProcessedDocumentSchema.cs` only — the form rebuilds
itself. Full procedure in
[CUSTOMIZING-SCHEMA.md](CUSTOMIZING-SCHEMA.md).

## Related code

- [`Review.razor`](../src/DocumentOcr.WebApp/Components/Pages/Review.razor) — page markup + handlers.
- [`ReviewUiHelpers`](../src/DocumentOcr.WebApp/Services/ReviewUiHelpers.cs) — formatting + `IsDateField`.
- [`DocumentReviewService`](../src/DocumentOcr.Common/Services/DocumentReviewService.cs) — per-field state machine, ETag-guarded writes.
- [`DocumentLockService`](../src/DocumentOcr.Common/Services/DocumentLockService.cs) — pessimistic checkout with 24 h staleness.
- [`PdfController`](../src/DocumentOcr.WebApp/Controllers/PdfController.cs) — streams the consolidated PDF for the in-page pdf.js viewer.
