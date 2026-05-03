using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using Microsoft.Extensions.Logging;

namespace DocumentOcr.Common.Services;

/// <summary>
/// T027 + T037 — Per-field save logic, state-machine enforcement, and the
/// implicit Pending → Reviewed transition. Persists via ETag-conditional
/// replace. Does NOT own checkout/check-in.
/// </summary>
public class DocumentReviewService : IDocumentReviewService
{
    private readonly ICosmosDbService _cosmos;
    private readonly ILogger<DocumentReviewService> _logger;
    private readonly Func<DateTime> _clock;

    public DocumentReviewService(
        ICosmosDbService cosmos,
        ILogger<DocumentReviewService> logger)
        : this(cosmos, logger, () => DateTime.UtcNow)
    {
    }

    /// <summary>Test-friendly constructor.</summary>
    public DocumentReviewService(
        ICosmosDbService cosmos,
        ILogger<DocumentReviewService> logger,
        Func<DateTime> clock)
    {
        _cosmos = cosmos;
        _logger = logger;
        _clock = clock;
    }

    public async Task<DocumentOcrEntity> ApplyEditsAsync(
        string documentId,
        string partitionKey,
        IReadOnlyDictionary<string, FieldEdit> edits,
        string reviewerUpn,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reviewerUpn))
        {
            throw new ArgumentException("Reviewer UPN must be provided.", nameof(reviewerUpn));
        }

        var entity = await _cosmos.GetDocumentByIdAsync(documentId, partitionKey)
            ?? throw new InvalidOperationException($"Document '{documentId}' not found.");

        var now = _clock();

        foreach (var (fieldName, edit) in edits)
        {
            if (!ProcessedDocumentSchema.FieldNames.Contains(fieldName))
            {
                throw new InvalidOperationException(
                    $"Field '{fieldName}' is not part of the reviewable schema.");
            }

            if (!entity.Schema.TryGetValue(fieldName, out var current))
            {
                throw new InvalidOperationException(
                    $"Document is missing schema entry for '{fieldName}'.");
            }

            ApplyFieldEdit(current, edit, reviewerUpn, now, fieldName);
        }

        // FR-017 / FR-018 — recompute ReviewStatus.
        var allReviewed = entity.Schema.Values.All(f => f.FieldStatus != SchemaFieldStatus.Pending);
        if (allReviewed && entity.ReviewStatus == ReviewStatus.Pending)
        {
            entity.ReviewStatus = ReviewStatus.Reviewed;
            entity.ReviewedBy = reviewerUpn;
            entity.ReviewedAt = now;
            _logger.LogInformation(
                "Document {Id} transitioned Pending\u2192Reviewed by {Reviewer}",
                entity.Id, reviewerUpn);
        }

        return await _cosmos.ReplaceWithETagAsync(entity, cancellationToken);
    }

    private static void ApplyFieldEdit(
        SchemaField current,
        FieldEdit edit,
        string reviewerUpn,
        DateTime now,
        string fieldName)
    {
        // Disallow OCR mutations.
        // The edit doesn't carry OcrValue/OcrConfidence — guard via state transition rules.

        switch (edit.NewStatus)
        {
            case SchemaFieldStatus.Pending:
                throw new InvalidOperationException(
                    $"Cannot transition '{fieldName}' back to Pending.");

            case SchemaFieldStatus.Confirmed:
                if (edit.NewReviewedValue is not null && !ValuesEqual(edit.NewReviewedValue, current.OcrValue))
                {
                    throw new InvalidOperationException(
                        $"Confirmed edit for '{fieldName}' must omit ReviewedValue or supply OcrValue.");
                }
                current.FieldStatus = SchemaFieldStatus.Confirmed;
                current.ReviewedValue = null;
                current.ReviewedBy = reviewerUpn;
                current.ReviewedAt = now;
                break;

            case SchemaFieldStatus.Corrected:
                if (edit.NewReviewedValue is null)
                {
                    throw new InvalidOperationException(
                        $"Corrected edit for '{fieldName}' requires a non-null ReviewedValue.");
                }
                if (ProcessedDocumentSchema.IsDateField(fieldName))
                {
                    ValidateDateReviewedValue(edit.NewReviewedValue, fieldName, now);
                }
                if (ValuesEqual(edit.NewReviewedValue, current.OcrValue))
                {
                    throw new InvalidOperationException(
                        $"Corrected edit for '{fieldName}' must differ from OcrValue (use Confirmed instead).");
                }
                current.FieldStatus = SchemaFieldStatus.Corrected;
                current.ReviewedValue = edit.NewReviewedValue;
                current.ReviewedBy = reviewerUpn;
                current.ReviewedAt = now;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown SchemaFieldStatus value: {edit.NewStatus}.");
        }

        current.EnsureValid();
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-002a: a reviewer-supplied date value MUST be a valid ISO
    /// <c>yyyy-MM-dd</c> string and MUST NOT be in the future (UTC).
    /// </summary>
    private static void ValidateDateReviewedValue(object reviewedValue, string fieldName, DateTime now)
    {
        var s = reviewedValue.ToString();
        if (string.IsNullOrWhiteSpace(s) ||
            !DateOnly.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
        {
            throw new InvalidOperationException(
                $"Date field '{fieldName}' requires a value in yyyy-MM-dd format; got '{s}'.");
        }

        var today = DateOnly.FromDateTime(now);
        if (date > today)
        {
            throw new InvalidOperationException(
                $"Date field '{fieldName}' cannot be in the future (got {s}, today is {today:yyyy-MM-dd}).");
        }
    }
}
