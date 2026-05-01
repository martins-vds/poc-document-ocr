# Contract: `IDocumentLockService`

**Project**: `DocumentOcr.Common.Interfaces`
**Consumed by**: `DocumentOcr.WebApp.Controllers.ReviewController`

Owns checkout / check-in / cancel-checkout and the 24-hour stale-checkout opportunistic release (D5).

```csharp
public interface IDocumentLockService
{
    /// <summary>
    /// Attempt to acquire an exclusive checkout for the given document.
    /// Succeeds when the record is not checked out, OR when it is held by the
    /// same caller, OR when the existing checkout is older than
    /// <see cref="DocumentLockDefaults.StaleCheckoutThreshold"/> (in which case
    /// the previous holder is opportunistically released first).
    /// </summary>
    Task<CheckoutResult> TryCheckoutAsync(
        string documentId,
        string partitionKey,
        string reviewerUpn,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// End the calling reviewer's checkout. Always stamps
    /// <c>LastCheckedInBy</c> / <c>LastCheckedInAt</c>.
    /// </summary>
    Task<DocumentOcrEntity> CheckinAsync(
        string documentId,
        string partitionKey,
        string reviewerUpn,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discard the calling reviewer's in-progress checkout WITHOUT updating
    /// <c>LastCheckedInBy</c> / <c>LastCheckedInAt</c> (FR-024). Per-field
    /// edits already saved via <see cref="IDocumentReviewService"/> remain.
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
/// instance-property bodies in interfaces; making it a static helper class
/// also lets the value be overridden by configuration in a single place.
/// </summary>
public static class DocumentLockDefaults
{
    /// <summary>24h per FR-022 / D5.</summary>
    public static readonly TimeSpan StaleCheckoutThreshold = TimeSpan.FromHours(24);
}

public sealed record CheckoutResult(
    bool Acquired,
    DocumentOcrEntity? Document,
    string? HeldBy,
    DateTime? HeldAt);
```

## Contract guarantees

1. **Mutual exclusion** (FR-021): `TryCheckoutAsync` returns `Acquired=false` when another reviewer holds a non-stale checkout. The result includes `HeldBy` / `HeldAt` for the UI to render the conflict message (FR-025).
2. **Idempotent re-checkout by the same holder**: calling `TryCheckoutAsync` while already holding the lock succeeds without changing `CheckedOutAt`.
3. **Stale auto-release** (FR-022): when `CheckedOutAt` is older than `DocumentLockDefaults.StaleCheckoutThreshold`, the existing checkout is cleared and the new caller's checkout is set in the **same** ETag-conditional write. A warning-level log entry MUST be emitted with the original holder and timestamps (asserted by `TryCheckout_StaleAutoRelease_LogsWarning...` in `DocumentLockServiceTests`).
4. **Check-in stamps** (FR-023, Q10): `CheckinAsync` clears `CheckedOutBy` / `CheckedOutAt`, sets `LastCheckedInBy = reviewerUpn` and `LastCheckedInAt = DateTime.UtcNow`, but does NOT touch `ReviewedBy` / `ReviewedAt`.
5. **Cancel does not stamp check-in** (FR-024): `CancelCheckoutAsync` clears `CheckedOutBy` / `CheckedOutAt` only; it MUST NOT update `LastCheckedInBy` / `LastCheckedInAt`.
6. **Authorization is the caller's job**: `CheckinAsync` and `CancelCheckoutAsync` raise `InvalidOperationException` if the entity is not currently checked out by `reviewerUpn`. The controller surfaces this as `403 Forbidden`.
7. **Concurrency**: all writes are ETag-conditional. A 412 response is retried at most once after re-reading the entity (this single re-read is the SDK-recommended pattern for optimistic concurrency on read-modify-write and is documented in [plan.md](../plan.md) § Complexity Tracking as a deviation from Constitution Principle IV's bespoke-retry prohibition). On second failure the controller returns `409 Conflict` with `error: "ConcurrentModification"`.

## Test cases

| Test                                                                      | Asserts     |
| ------------------------------------------------------------------------- | ----------- |
| `TryCheckout_FreeRecord_Succeeds`                                         | guarantee 1 |
| `TryCheckout_HeldByOther_ReturnsAcquiredFalseWithHolderInfo`              | guarantee 1 |
| `TryCheckout_HeldBySameCaller_IsIdempotent`                               | guarantee 2 |
| `TryCheckout_HeldButOlderThan24h_AutoReleasesAndAcquires`                 | guarantee 3 |
| `Checkin_StampsLastCheckedInButNotReviewedAt`                             | guarantee 4 |
| `Cancel_DoesNotStampLastCheckedIn`                                        | guarantee 5 |
| `Checkin_NotCurrentHolder_Throws`                                         | guarantee 6 |
| `Checkin_ETagConflict_RetriesOnceThenSurfaces`                            | guarantee 7 |
| `TryCheckout_StaleAutoRelease_LogsWarningWithOriginalHolderAndTimestamps` | guarantee 3 |
