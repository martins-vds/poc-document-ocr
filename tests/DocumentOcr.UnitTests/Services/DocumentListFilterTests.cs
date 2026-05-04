using DocumentOcr.Common.Models;
using DocumentOcr.Common.Services;

namespace DocumentOcr.UnitTests.Services;

/// <summary>
/// T045a — DocumentListFilter helper tests.
/// </summary>
public class DocumentListFilterTests
{
    private static DocumentOcrEntity Doc(string id, ReviewStatus status, string? checkedOutBy)
    {
        var schema = new Dictionary<string, SchemaField>(StringComparer.Ordinal);
        foreach (var name in ProcessedDocumentSchema.FieldNames)
        {
            schema[name] = SchemaField.CreateInitial(null, null);
        }
        return new DocumentOcrEntity
        {
            Id = id,
            Identifier = id,
            ReviewStatus = status,
            CheckedOutBy = checkedOutBy,
            Schema = schema,
        };
    }

    [Fact]
    public void Filter_NoFilters_ReturnsAll()
    {
        var docs = new[] { Doc("a", ReviewStatus.Pending, null), Doc("b", ReviewStatus.Reviewed, "x") };

        var result = DocumentListFilter.Filter(docs, null, DocumentListFilter.CheckoutFilter.All).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Filter_ByStatus_FiltersOnReviewStatus()
    {
        var docs = new[] { Doc("a", ReviewStatus.Pending, null), Doc("b", ReviewStatus.Reviewed, null) };

        var result = DocumentListFilter.Filter(docs, ReviewStatus.Reviewed, DocumentListFilter.CheckoutFilter.All).ToList();

        Assert.Single(result);
        Assert.Equal("b", result[0].Id);
    }

    [Fact]
    public void Filter_CheckedOut_FiltersToHeldDocuments()
    {
        var docs = new[] { Doc("a", ReviewStatus.Pending, null), Doc("b", ReviewStatus.Pending, "x") };

        var result = DocumentListFilter.Filter(docs, null, DocumentListFilter.CheckoutFilter.CheckedOut).ToList();

        Assert.Single(result);
        Assert.Equal("b", result[0].Id);
    }

    [Fact]
    public void Filter_Free_FiltersToFreeDocuments()
    {
        var docs = new[] { Doc("a", ReviewStatus.Pending, null), Doc("b", ReviewStatus.Pending, "x") };

        var result = DocumentListFilter.Filter(docs, null, DocumentListFilter.CheckoutFilter.Free).ToList();

        Assert.Single(result);
        Assert.Equal("a", result[0].Id);
    }

    [Fact]
    public void CountFieldsReviewed_CountsNonPending()
    {
        var doc = Doc("a", ReviewStatus.Pending, null);
        doc.Schema["accusedName"].FieldStatus = SchemaFieldStatus.Confirmed;
        doc.Schema["mainCharge"].FieldStatus = SchemaFieldStatus.Corrected;
        doc.Schema["mainCharge"].ReviewedValue = "fixed";

        var count = DocumentListFilter.CountFieldsReviewed(doc);

        Assert.Equal(2, count);
    }
}
