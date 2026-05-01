using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace DocumentOcr.Common.Services;

/// <summary>
/// T036 — Owns checkout / check-in / cancel-checkout per spec FR-021..FR-024.
/// All persistence uses ETag-conditional replace; the 24h stale-checkout
/// threshold (<see cref="DocumentLockDefaults.StaleCheckoutThreshold"/>)
/// allows opportunistic acquisition.
/// </summary>
public class DocumentLockService : IDocumentLockService
{
    private readonly ICosmosDbService _cosmos;
    private readonly ILogger<DocumentLockService> _logger;
    private readonly Func<DateTime> _clock;

    public DocumentLockService(ICosmosDbService cosmos, ILogger<DocumentLockService> logger)
        : this(cosmos, logger, () => DateTime.UtcNow)
    {
    }

    public DocumentLockService(ICosmosDbService cosmos, ILogger<DocumentLockService> logger, Func<DateTime> clock)
    {
        _cosmos = cosmos;
        _logger = logger;
        _clock = clock;
    }

    public async Task<CheckoutResult> TryCheckoutAsync(
        string documentId,
        string partitionKey,
        string reviewerUpn,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reviewerUpn))
        {
            throw new ArgumentException("Reviewer UPN required.", nameof(reviewerUpn));
        }

        var entity = await _cosmos.GetDocumentByIdAsync(documentId, partitionKey)
            ?? throw new InvalidOperationException($"Document '{documentId}' not found.");

        var now = _clock();

        if (entity.CheckedOutBy is not null && entity.CheckedOutAt is not null)
        {
            var heldBy = entity.CheckedOutBy;
            var heldAt = entity.CheckedOutAt.Value;

            if (string.Equals(heldBy, reviewerUpn, StringComparison.OrdinalIgnoreCase))
            {
                // Same reviewer — refresh timestamp.
                entity.CheckedOutAt = now;
                var refreshed = await ReplaceWithSingleRetry(entity, cancellationToken);
                return new CheckoutResult(true, refreshed, refreshed.CheckedOutBy, refreshed.CheckedOutAt);
            }

            var age = now - heldAt;
            if (age < DocumentLockDefaults.StaleCheckoutThreshold)
            {
                _logger.LogInformation(
                    "Checkout for {DocumentId} denied; held by {HeldBy} for {Age}.",
                    documentId, heldBy, age);
                return new CheckoutResult(false, entity, heldBy, heldAt);
            }

            _logger.LogWarning(
                "Stale checkout on {DocumentId} held by {HeldBy} for {Age}; releasing for {NewReviewer}.",
                documentId, heldBy, age, reviewerUpn);
        }

        entity.CheckedOutBy = reviewerUpn;
        entity.CheckedOutAt = now;
        var saved = await ReplaceWithSingleRetry(entity, cancellationToken);
        return new CheckoutResult(true, saved, saved.CheckedOutBy, saved.CheckedOutAt);
    }

    public async Task<DocumentOcrEntity> CheckinAsync(
        string documentId,
        string partitionKey,
        string reviewerUpn,
        CancellationToken cancellationToken = default)
    {
        var entity = await _cosmos.GetDocumentByIdAsync(documentId, partitionKey)
            ?? throw new InvalidOperationException($"Document '{documentId}' not found.");

        if (entity.CheckedOutBy is not null &&
            !string.Equals(entity.CheckedOutBy, reviewerUpn, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Document '{documentId}' is checked out by '{entity.CheckedOutBy}', not '{reviewerUpn}'.");
        }

        var now = _clock();
        entity.CheckedOutBy = null;
        entity.CheckedOutAt = null;
        entity.LastCheckedInBy = reviewerUpn;
        entity.LastCheckedInAt = now;

        return await ReplaceWithSingleRetry(entity, cancellationToken);
    }

    public async Task<DocumentOcrEntity> CancelCheckoutAsync(
        string documentId,
        string partitionKey,
        string reviewerUpn,
        CancellationToken cancellationToken = default)
    {
        var entity = await _cosmos.GetDocumentByIdAsync(documentId, partitionKey)
            ?? throw new InvalidOperationException($"Document '{documentId}' not found.");

        if (entity.CheckedOutBy is not null &&
            !string.Equals(entity.CheckedOutBy, reviewerUpn, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Document '{documentId}' is checked out by '{entity.CheckedOutBy}', not '{reviewerUpn}'.");
        }

        // FR-024 — clear checkout WITHOUT updating LastCheckedIn stamps.
        entity.CheckedOutBy = null;
        entity.CheckedOutAt = null;

        return await ReplaceWithSingleRetry(entity, cancellationToken);
    }

    private async Task<DocumentOcrEntity> ReplaceWithSingleRetry(DocumentOcrEntity entity, CancellationToken cancellationToken)
    {
        try
        {
            return await _cosmos.ReplaceWithETagAsync(entity, cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            _logger.LogInformation("ETag conflict on {DocumentId}; retrying once.", entity.Id);
            var fresh = await _cosmos.GetDocumentByIdAsync(entity.Id, entity.Identifier)
                ?? throw new InvalidOperationException($"Document '{entity.Id}' vanished during retry.");
            // Re-apply the lock fields from the in-flight entity onto the fresh copy.
            fresh.CheckedOutBy = entity.CheckedOutBy;
            fresh.CheckedOutAt = entity.CheckedOutAt;
            fresh.LastCheckedInBy = entity.LastCheckedInBy;
            fresh.LastCheckedInAt = entity.LastCheckedInAt;
            return await _cosmos.ReplaceWithETagAsync(fresh, cancellationToken);
        }
    }
}
