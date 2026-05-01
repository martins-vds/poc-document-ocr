# Contract: `IDocumentReviewService`

**Project**: `DocumentOcr.Common.Interfaces`
**Consumed by**: `DocumentOcr.WebApp.Controllers.ReviewController` (and `Review.razor` indirectly)

Owns the per-field save logic and the implicit `Pending → Reviewed` transition. Does **not** own checkout/check-in (see [`IDocumentLockService.md`](IDocumentLockService.md)). Authorization (caller is the current holder) is enforced by `ReviewController`; this service trusts its callers but validates invariants.

```csharp
public interface IDocumentReviewService
{
    /// <summary>
    /// Apply a set of per-field edits to a checked-out document, recompute
    /// <c>ReviewStatus</c>, and persist via Cosmos ETag-conditional replace.
    /// Does NOT change checkout state.
    /// </summary>
    /// <param name="documentId">Cosmos document id.</param>
    /// <param name="partitionKey">Partition key (the identifier).</param>
    /// <param name="edits">
    /// Map of camelCase field name → desired new state. Only the fields
    /// present in this map are touched; omitted fields keep their current
    /// state. Each edit specifies the new <c>FieldStatus</c> and (for
    /// <c>Corrected</c>) the new <c>ReviewedValue</c>.
    /// </param>
    /// <param name="reviewerUpn">UPN of the calling reviewer.</param>
    /// <returns>The updated entity (with refreshed ETag).</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an edit attempts to mutate <c>OcrValue</c> or
    /// <c>OcrConfidence</c>, or violates the field-status state machine,
    /// or names a field not in <see cref="ProcessedDocumentSchema.FieldNames"/>.
    /// </exception>
    /// <exception cref="CosmosException">
    /// Propagated when the ETag-conditional replace fails (HTTP 412).
    /// </exception>
    Task<DocumentOcrEntity> ApplyEditsAsync(
        string documentId,
        string partitionKey,
        IReadOnlyDictionary<string, FieldEdit> edits,
        string reviewerUpn,
        CancellationToken cancellationToken = default);
}

public sealed record FieldEdit(SchemaFieldStatus NewStatus, object? NewReviewedValue);
```

## Contract guarantees

1. **OCR provenance immutable** (FR-014): any edit that would change `OcrValue` or `OcrConfidence` raises `InvalidOperationException` before any Cosmos write.
2. **State-machine enforcement** (FR-016): edits must satisfy the validation rules in `data-model.md` § `SchemaField`. Violations raise `InvalidOperationException`.
3. **Per-field stamping** (FR-015): for each touched field, `ReviewedAt = DateTime.UtcNow` and `ReviewedBy = reviewerUpn`. Untouched fields are not re-stamped.
4. **Record-level recomputation** (FR-017): after applying edits, `ReviewStatus = Reviewed` iff every `Schema[*].FieldStatus != Pending`; else `Pending`.
5. **First-transition stamping** (FR-018): if recomputation flips `ReviewStatus` from `Pending` to `Reviewed` for the first time, `ReviewedBy = reviewerUpn` and `ReviewedAt = DateTime.UtcNow` on the entity itself, and these become immutable (subsequent edits never re-stamp them).
6. **Persistence**: writes via `ICosmosDbService.UpdateDocumentAsync` using the entity's current `ETag`. A concurrent modification surfaces as `CosmosException` (HTTP 412), which the controller maps to `409 Conflict` with a "record changed under you" message.
7. **No checkout side-effects**: the entity's `CheckedOutBy` / `CheckedOutAt` are not modified. (Check-in is a separate `IDocumentLockService` call.)

## Test cases

| Test                                                               | Asserts     |
| ------------------------------------------------------------------ | ----------- |
| `ApplyEdits_AttemptToChangeOcrValue_Throws`                        | guarantee 1 |
| `ApplyEdits_PendingToCorrectedWithoutReviewedValue_Throws`         | guarantee 2 |
| `ApplyEdits_StampsReviewedAtAndReviewedByOnTouchedFieldsOnly`      | guarantee 3 |
| `ApplyEdits_LastPendingFieldResolved_FlipsRecordToReviewed`        | guarantee 4 |
| `ApplyEdits_FirstTransitionToReviewed_StampsRecordReviewedByAndAt` | guarantee 5 |
| `ApplyEdits_SecondTransitionToReviewed_DoesNotRestampRecord`       | guarantee 5 |
| `ApplyEdits_StaleETag_Throws409Conflict`                           | guarantee 6 |
| `ApplyEdits_DoesNotMutateCheckoutFields`                           | guarantee 7 |
