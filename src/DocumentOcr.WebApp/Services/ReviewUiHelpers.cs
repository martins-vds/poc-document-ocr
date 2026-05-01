using DocumentOcr.Common.Models;

namespace DocumentOcr.WebApp.Services;

/// <summary>
/// Confidence band derived from <see cref="SchemaField.OcrConfidence"/>.
/// Drives row-level color coding on the Review page.
/// </summary>
public enum ConfidenceBand
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

/// <summary>
/// Pure helper functions used by Review.razor. Extracted so the per-field
/// display decisions (confidence band, prefilled value, page anchor) can be
/// unit-tested without spinning up a Blazor renderer.
/// </summary>
public static class ReviewUiHelpers
{
    private const double HighThreshold = 0.85;
    private const double MediumThreshold = 0.60;

    public static ConfidenceBand GetConfidenceBand(double? confidence)
    {
        if (confidence is null)
        {
            return ConfidenceBand.Unknown;
        }

        var c = confidence.Value;
        if (c >= HighThreshold) return ConfidenceBand.High;
        if (c >= MediumThreshold) return ConfidenceBand.Medium;
        return ConfidenceBand.Low;
    }

    public static string GetConfidenceCssClass(ConfidenceBand band) => band switch
    {
        ConfidenceBand.High => "confidence-high",
        ConfidenceBand.Medium => "confidence-medium",
        ConfidenceBand.Low => "confidence-low",
        _ => "confidence-unknown",
    };

    /// <summary>
    /// What to show in the "Reviewed value" display column.
    /// Pending fields show empty (the reviewer has not yet acted).
    /// Confirmed/Corrected fields show their <see cref="SchemaField.ReviewedValue"/>.
    /// </summary>
    public static string GetReviewedValueDisplay(SchemaField? field)
    {
        if (field is null || field.FieldStatus == SchemaFieldStatus.Pending)
        {
            return string.Empty;
        }

        return field.ReviewedValue?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Initial value for the editable textbox. Prefilled with the OCR value
    /// for Pending fields so the reviewer can confirm with a single click,
    /// or with the previously-saved reviewed value for Confirmed/Corrected.
    /// </summary>
    public static string GetEditableInitialValue(SchemaField? field)
    {
        if (field is null)
        {
            return string.Empty;
        }

        if (field.FieldStatus != SchemaFieldStatus.Pending && field.ReviewedValue is not null)
        {
            return field.ReviewedValue.ToString() ?? string.Empty;
        }

        return field.OcrValue?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Build the URL for the in-page PDF viewer. Appends the
    /// <c>#page=N</c> fragment understood by browser PDF renderers.
    /// </summary>
    public static string GetPdfUrl(string documentId, string identifier, int? page)
    {
        var encodedId = Uri.EscapeDataString(documentId);
        var encodedIdentifier = Uri.EscapeDataString(identifier);
        var baseUrl = $"/api/pdf/{encodedId}/{encodedIdentifier}";
        return page is null ? baseUrl : $"{baseUrl}#page={page.Value}";
    }

    /// <summary>
    /// Combined CSS class for a field row: <c>field-row</c> plus
    /// confidence-band class plus status class. Drives left-border color
    /// (confidence) and background tint (status).
    /// </summary>
    public static string GetFieldRowCssClass(SchemaField? field)
    {
        var band = GetConfidenceBand(field?.OcrConfidence);
        var status = (field?.FieldStatus ?? SchemaFieldStatus.Pending) switch
        {
            SchemaFieldStatus.Confirmed => "status-confirmed",
            SchemaFieldStatus.Corrected => "status-corrected",
            _ => "status-pending",
        };
        return $"field-row {GetConfidenceCssClass(band)} {status}";
    }

    /// <summary>
    /// First page in the processed PDF where the entity's identifier appears,
    /// or the first inferred page as a fallback. Used to position the PDF
    /// viewer on initial load.
    /// </summary>
    public static int? GetPrimaryPageNumber(DocumentOcrEntity? entity)
    {
        if (entity is null || entity.PageProvenance.Count == 0)
        {
            return null;
        }

        var match = entity.PageProvenance
            .FirstOrDefault(p => p.IdentifierSource == IdentifierSource.Extracted
                              && string.Equals(p.ExtractedIdentifier, entity.Identifier, StringComparison.Ordinal));

        return match?.PageNumber ?? entity.PageProvenance[0].PageNumber;
    }
}
