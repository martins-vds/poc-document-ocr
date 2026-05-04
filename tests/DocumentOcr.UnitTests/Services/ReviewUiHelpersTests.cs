using DocumentOcr.Common.Models;
using DocumentOcr.WebApp.Services;

namespace DocumentOcr.UnitTests.Services;

/// <summary>
/// TDD tests for review-page UI helper logic. Pure functions extracted
/// from Review.razor so the per-field display decisions can be tested
/// without spinning up a Blazor renderer.
/// </summary>
public class ReviewUiHelpersTests
{
    // ── Confidence bands ────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0, ConfidenceBand.High)]
    [InlineData(0.95, ConfidenceBand.High)]
    [InlineData(0.85, ConfidenceBand.High)]
    [InlineData(0.8499, ConfidenceBand.Medium)]
    [InlineData(0.70, ConfidenceBand.Medium)]
    [InlineData(0.60, ConfidenceBand.Medium)]
    [InlineData(0.5999, ConfidenceBand.Low)]
    [InlineData(0.30, ConfidenceBand.Low)]
    [InlineData(0.0, ConfidenceBand.Low)]
    public void GetConfidenceBand_ClassifiesByThreshold(double confidence, ConfidenceBand expected)
    {
        Assert.Equal(expected, ReviewUiHelpers.GetConfidenceBand(confidence));
    }

    [Fact]
    public void GetConfidenceBand_NullConfidence_ReturnsUnknown()
    {
        Assert.Equal(ConfidenceBand.Unknown, ReviewUiHelpers.GetConfidenceBand(null));
    }

    [Theory]
    [InlineData(ConfidenceBand.High, "confidence-high")]
    [InlineData(ConfidenceBand.Medium, "confidence-medium")]
    [InlineData(ConfidenceBand.Low, "confidence-low")]
    [InlineData(ConfidenceBand.Unknown, "confidence-unknown")]
    public void GetConfidenceCssClass_MapsBandToCssClass(ConfidenceBand band, string expected)
    {
        Assert.Equal(expected, ReviewUiHelpers.GetConfidenceCssClass(band));
    }

    // ── Reviewed-value display ──────────────────────────────────────────

    [Fact]
    public void GetReviewedValueDisplay_PendingField_ReturnsEmptyPlaceholder()
    {
        var field = SchemaField.CreateInitial(ocrValue: "ocr-text", ocrConfidence: 0.9);

        var display = ReviewUiHelpers.GetReviewedValueDisplay(field);

        Assert.Equal(string.Empty, display);
    }

    [Fact]
    public void GetReviewedValueDisplay_ConfirmedField_ReturnsOcrValue()
    {
        // Confirmed fields persist ReviewedValue == OcrValue (per data-model invariant).
        var field = new SchemaField
        {
            OcrValue = "abc123",
            OcrConfidence = 0.9,
            ReviewedValue = "abc123",
            ReviewedAt = DateTime.UtcNow,
            ReviewedBy = "user@example.com",
            FieldStatus = SchemaFieldStatus.Confirmed,
        };

        Assert.Equal("abc123", ReviewUiHelpers.GetReviewedValueDisplay(field));
    }

    [Fact]
    public void GetReviewedValueDisplay_CorrectedField_ReturnsCorrectedValue()
    {
        var field = new SchemaField
        {
            OcrValue = "wrong",
            OcrConfidence = 0.5,
            ReviewedValue = "right",
            ReviewedAt = DateTime.UtcNow,
            ReviewedBy = "user@example.com",
            FieldStatus = SchemaFieldStatus.Corrected,
        };

        Assert.Equal("right", ReviewUiHelpers.GetReviewedValueDisplay(field));
    }

    [Fact]
    public void GetReviewedValueDisplay_NullField_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ReviewUiHelpers.GetReviewedValueDisplay(null));
    }

    // ── Editable initial value (used to pre-fill the textbox) ───────────

    [Fact]
    public void GetEditableInitialValue_PendingField_PrefillsWithOcrValue()
    {
        var field = SchemaField.CreateInitial("ocr-text", 0.9);
        Assert.Equal("ocr-text", ReviewUiHelpers.GetEditableInitialValue(field));
    }

    [Fact]
    public void GetEditableInitialValue_CorrectedField_PrefillsWithReviewedValue()
    {
        var field = new SchemaField
        {
            OcrValue = "wrong",
            ReviewedValue = "right",
            FieldStatus = SchemaFieldStatus.Corrected,
        };
        Assert.Equal("right", ReviewUiHelpers.GetEditableInitialValue(field));
    }

    [Fact]
    public void GetEditableInitialValue_NullField_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ReviewUiHelpers.GetEditableInitialValue(null));
    }

    // ── PDF page anchor ─────────────────────────────────────────────────

    [Fact]
    public void GetPdfUrl_NoPage_ReturnsBaseUrl()
    {
        var url = ReviewUiHelpers.GetPdfUrl("doc-1", "ABC-123", page: null);
        Assert.Equal("/api/pdf/doc-1/ABC-123", url);
    }

    [Fact]
    public void GetPdfUrl_WithPage_AppendsPageFragment()
    {
        var url = ReviewUiHelpers.GetPdfUrl("doc-1", "ABC-123", page: 3);
        Assert.Equal("/api/pdf/doc-1/ABC-123#page=3", url);
    }

    [Fact]
    public void GetPdfUrl_UrlEncodesIdentifier()
    {
        var url = ReviewUiHelpers.GetPdfUrl("doc-1", "A B/C", page: null);
        Assert.Equal("/api/pdf/doc-1/A%20B%2FC", url);
    }

    // ── Field-row CSS (combines status + confidence) ────────────────────

    [Fact]
    public void GetFieldRowCssClass_PendingHighConfidence_HighlightsConfidenceOnly()
    {
        var field = SchemaField.CreateInitial("v", 0.95);
        Assert.Equal("field-row confidence-high status-pending", ReviewUiHelpers.GetFieldRowCssClass(field));
    }

    [Fact]
    public void GetFieldRowCssClass_ConfirmedField_AddsConfirmedStatus()
    {
        var field = new SchemaField { OcrValue = "v", OcrConfidence = 0.5, ReviewedValue = "v", ReviewedAt = DateTime.UtcNow, ReviewedBy = "u", FieldStatus = SchemaFieldStatus.Confirmed };
        Assert.Equal("field-row confidence-low status-confirmed", ReviewUiHelpers.GetFieldRowCssClass(field));
    }

    [Fact]
    public void GetFieldRowCssClass_CorrectedField_AddsCorrectedStatus()
    {
        var field = new SchemaField { OcrValue = "x", OcrConfidence = 0.7, ReviewedValue = "y", ReviewedAt = DateTime.UtcNow, ReviewedBy = "u", FieldStatus = SchemaFieldStatus.Corrected };
        Assert.Equal("field-row confidence-medium status-corrected", ReviewUiHelpers.GetFieldRowCssClass(field));
    }

    [Fact]
    public void GetFieldRowCssClass_NullField_ReturnsBaseClass()
    {
        Assert.Equal("field-row confidence-unknown status-pending", ReviewUiHelpers.GetFieldRowCssClass(null));
    }

    // ── Page-number lookup from provenance (LOCAL page in per-identifier PDF) ──

    [Fact]
    public void GetPrimaryPageNumber_ReturnsLocalIndexOfFirstExtractedPage()
    {
        // Original PDF had pages 28, 29, 30 belong to identifier ABC.
        // The aggregated per-identifier PDF only contains those 3 pages,
        // numbered 1, 2, 3 locally. The reviewer must see local page 1
        // (where the identifier was first extracted), not original 28.
        var entity = new DocumentOcrEntity
        {
            Identifier = "ABC",
            PageNumbers = new List<int> { 28, 29, 30 },
            PageProvenance = new List<PageProvenanceEntry>
            {
                PageProvenanceEntry.Extracted(28, "ABC"),
                PageProvenanceEntry.Extracted(29, "ABC"),
                PageProvenanceEntry.Extracted(30, "ABC"),
            },
        };

        Assert.Equal(1, ReviewUiHelpers.GetPrimaryPageNumber(entity));
    }

    [Fact]
    public void GetPrimaryPageNumber_ExtractedAfterInferred_ReturnsLocalPageOfFirstExtracted()
    {
        // Original pages 5,6,7 — page 5 was forward-filled (Inferred),
        // page 6 had the extracted identifier. Locally, page 6 is index 2.
        var entity = new DocumentOcrEntity
        {
            Identifier = "ABC",
            PageNumbers = new List<int> { 5, 6, 7 },
            PageProvenance = new List<PageProvenanceEntry>
            {
                PageProvenanceEntry.Inferred(5),
                PageProvenanceEntry.Extracted(6, "ABC"),
                PageProvenanceEntry.Extracted(7, "ABC"),
            },
        };

        Assert.Equal(2, ReviewUiHelpers.GetPrimaryPageNumber(entity));
    }

    [Fact]
    public void GetPrimaryPageNumber_OnlyInferred_ReturnsLocalPage1()
    {
        var entity = new DocumentOcrEntity
        {
            Identifier = "ABC",
            PageNumbers = new List<int> { 5, 6 },
            PageProvenance = new List<PageProvenanceEntry>
            {
                PageProvenanceEntry.Inferred(5),
                PageProvenanceEntry.Inferred(6),
            },
        };

        Assert.Equal(1, ReviewUiHelpers.GetPrimaryPageNumber(entity));
    }

    [Fact]
    public void GetPrimaryPageNumber_EmptyProvenance_ReturnsNull()
    {
        var entity = new DocumentOcrEntity { Identifier = "ABC" };
        Assert.Null(ReviewUiHelpers.GetPrimaryPageNumber(entity));
    }

    [Fact]
    public void GetPrimaryPageNumber_NullEntity_ReturnsNull()
    {
        Assert.Null(ReviewUiHelpers.GetPrimaryPageNumber(null));
    }

    // ── Boolean field detection ─────────────────────────────────────────

    [Theory]
    [InlineData("judgeSignature", true)]
    [InlineData("endorsementSignature", true)]
    [InlineData("fileTkNumber", false)]
    [InlineData("mainCharge", false)]
    [InlineData("unknownField", false)]
    public void IsBooleanField_ReturnsTrueOnlyForSignatureFields(string name, bool expected)
    {
        Assert.Equal(expected, ReviewUiHelpers.IsBooleanField(name));
    }
}
