using DocumentOcr.Common.Models;

namespace DocumentOcr.Common.Interfaces;

/// <summary>
/// Per-field save logic and the implicit Pending → Reviewed transition.
/// Does NOT own checkout/check-in (see <see cref="IDocumentLockService"/>).
/// </summary>
public interface IDocumentReviewService
{
    /// <summary>
    /// Apply per-field edits, recompute <see cref="DocumentOcrEntity.ReviewStatus"/>,
    /// and persist via ETag-conditional replace. Does NOT touch checkout state.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When an edit attempts to mutate <c>OcrValue</c>/<c>OcrConfidence</c>,
    /// violates the field-status state machine, or names a field not in
    /// <see cref="ProcessedDocumentSchema.FieldNames"/>.
    /// </exception>
    Task<DocumentOcrEntity> ApplyEditsAsync(
        string documentId,
        string partitionKey,
        IReadOnlyDictionary<string, FieldEdit> edits,
        string reviewerUpn,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One per-field edit. Carries the desired new <see cref="SchemaFieldStatus"/>
/// and (for <c>Corrected</c>) the new reviewed value.
/// </summary>
public sealed record FieldEdit(SchemaFieldStatus NewStatus, object? NewReviewedValue);
