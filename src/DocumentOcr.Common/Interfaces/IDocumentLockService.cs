using DocumentOcr.Common.Models;

namespace DocumentOcr.Common.Interfaces;

/// <summary>
/// Owns checkout / check-in / cancel-checkout and the 24-hour stale-checkout
/// opportunistic release per spec FR-021..FR-024 and contracts/IDocumentLockService.md.
/// </summary>
public interface IDocumentLockService
{
    /// <summary>
    /// Attempt to acquire an exclusive checkout. Succeeds when free, when
    /// already held by the same caller, or when the existing checkout is
    /// older than <see cref="DocumentLockDefaults.StaleCheckoutThreshold"/>.
    /// </summary>
    Task<CheckoutResult> TryCheckoutAsync(
        string documentId,
        string partitionKey,
        string reviewerUpn,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// End the calling reviewer's checkout. Always stamps
    /// <see cref="DocumentOcrEntity.LastCheckedInBy"/> /
    /// <see cref="DocumentOcrEntity.LastCheckedInAt"/>.
    /// </summary>
    Task<DocumentOcrEntity> CheckinAsync(
        string documentId,
        string partitionKey,
        string reviewerUpn,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discard the calling reviewer's in-progress checkout WITHOUT updating
    /// the last-checked-in stamps (FR-024). Per-field edits saved via
    /// <see cref="IDocumentReviewService"/> are retained.
    /// </summary>
    Task<DocumentOcrEntity> CancelCheckoutAsync(
        string documentId,
        string partitionKey,
        string reviewerUpn,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defaults consumed by <see cref="IDocumentLockService"/> implementations.
/// Lifted out of the interface (analysis-fix F1) because C# does not allow
/// instance-property bodies in interfaces.
/// </summary>
public static class DocumentLockDefaults
{
    /// <summary>24h per FR-022 / D5.</summary>
    public static readonly TimeSpan StaleCheckoutThreshold = TimeSpan.FromHours(24);
}

/// <summary>Result of a <see cref="IDocumentLockService.TryCheckoutAsync"/> call.</summary>
public sealed record CheckoutResult(
    bool Acquired,
    DocumentOcrEntity? Document,
    string? HeldBy,
    DateTime? HeldAt);
