using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using DocumentOcr.Common.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentOcr.Tests.Services;

/// <summary>
/// T026 + T035 — covers DocumentReviewService invariants and the
/// FR-017 / FR-018 record-level Pending → Reviewed transition.
/// </summary>
public class DocumentReviewServiceTests
{
    private static readonly DateTime FixedClock = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private const string Reviewer = "alice@contoso.com";

    private static DocumentOcrEntity NewEntity(Action<Dictionary<string, SchemaField>>? customize = null)
    {
        var schema = new Dictionary<string, SchemaField>(StringComparer.Ordinal);
        foreach (var name in ProcessedDocumentSchema.FieldNames)
        {
            schema[name] = SchemaField.CreateInitial("ocr", 0.9);
        }
        customize?.Invoke(schema);
        return new DocumentOcrEntity
        {
            Id = "doc-1",
            Identifier = "TK-1",
            Schema = schema,
            ReviewStatus = ReviewStatus.Pending,
        };
    }

    private static (DocumentReviewService service, Mock<ICosmosDbService> cosmos) BuildService(DocumentOcrEntity entity)
    {
        var cosmos = new Mock<ICosmosDbService>();
        cosmos.Setup(c => c.GetDocumentByIdAsync("doc-1", "TK-1")).ReturnsAsync(entity);
        cosmos.Setup(c => c.ReplaceWithETagAsync(It.IsAny<DocumentOcrEntity>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((DocumentOcrEntity e, CancellationToken _) => e);
        var service = new DocumentReviewService(cosmos.Object, NullLogger<DocumentReviewService>.Instance, () => FixedClock);
        return (service, cosmos);
    }

    [Fact]
    public async Task ApplyEdits_ConfirmField_StampsReviewedByAndAt()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);

        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Confirmed, null),
        };

        var result = await service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer);

        var field = result.Schema["accusedName"];
        Assert.Equal(SchemaFieldStatus.Confirmed, field.FieldStatus);
        Assert.Equal(Reviewer, field.ReviewedBy);
        Assert.Equal(FixedClock, field.ReviewedAt);
        Assert.Null(field.ReviewedValue);
    }

    [Fact]
    public async Task ApplyEdits_CorrectField_RecordsNewValue()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);

        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Corrected, "Corrected Name"),
        };

        var result = await service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer);

        var field = result.Schema["accusedName"];
        Assert.Equal(SchemaFieldStatus.Corrected, field.FieldStatus);
        Assert.Equal("Corrected Name", field.ReviewedValue);
    }

    [Fact]
    public async Task ApplyEdits_CorrectedSameAsOcrValue_Throws()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);

        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Corrected, "ocr"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer));
    }

    [Fact]
    public async Task ApplyEdits_PendingTransition_Throws()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);

        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Pending, null),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer));
    }

    [Fact]
    public async Task ApplyEdits_UnknownField_Throws()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);

        var edits = new Dictionary<string, FieldEdit>
        {
            ["bogusField"] = new(SchemaFieldStatus.Confirmed, null),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer));
    }

    [Fact]
    public async Task ApplyEdits_FinalFieldReviewed_TransitionsRecordToReviewed()
    {
        // Pre-confirm 12 fields; one Pending remains.
        var entity = NewEntity(schema =>
        {
            foreach (var name in ProcessedDocumentSchema.FieldNames.Where(n => n != "accusedName"))
            {
                schema[name].FieldStatus = SchemaFieldStatus.Confirmed;
                schema[name].ReviewedBy = "earlier@contoso.com";
                schema[name].ReviewedAt = new DateTime(2025, 12, 1);
            }
        });
        var (service, _) = BuildService(entity);

        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Confirmed, null),
        };

        var result = await service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer);

        Assert.Equal(ReviewStatus.Reviewed, result.ReviewStatus);
        Assert.Equal(Reviewer, result.ReviewedBy);
        Assert.Equal(FixedClock, result.ReviewedAt);
    }

    [Fact]
    public async Task ApplyEdits_PartialReview_KeepsRecordPending()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);

        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Confirmed, null),
        };

        var result = await service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer);

        Assert.Equal(ReviewStatus.Pending, result.ReviewStatus);
        Assert.Null(result.ReviewedBy);
    }

    [Fact]
    public async Task ApplyEdits_AlreadyReviewed_DoesNotOverwriteReviewedStamp()
    {
        var existingReviewer = "first@contoso.com";
        var existingTime = new DateTime(2025, 12, 1, 8, 0, 0, DateTimeKind.Utc);
        var entity = NewEntity(schema =>
        {
            foreach (var name in ProcessedDocumentSchema.FieldNames)
            {
                schema[name].FieldStatus = SchemaFieldStatus.Confirmed;
            }
        });
        entity.ReviewStatus = ReviewStatus.Reviewed;
        entity.ReviewedBy = existingReviewer;
        entity.ReviewedAt = existingTime;
        var (service, _) = BuildService(entity);

        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Corrected, "tweak"),
        };

        var result = await service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer);

        Assert.Equal(existingReviewer, result.ReviewedBy);
        Assert.Equal(existingTime, result.ReviewedAt);
    }

    // ---------- Edge-case coverage ----------

    [Fact]
    public async Task ApplyEdits_BlankReviewer_ThrowsArgumentException()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);
        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Confirmed, null),
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ApplyEditsAsync("doc-1", "TK-1", edits, " "));
    }

    [Fact]
    public async Task ApplyEdits_DocumentMissing_Throws()
    {
        var cosmos = new Mock<ICosmosDbService>();
        cosmos.Setup(c => c.GetDocumentByIdAsync("doc-x", "TK-x")).ReturnsAsync((DocumentOcrEntity?)null);
        var service = new DocumentReviewService(cosmos.Object, NullLogger<DocumentReviewService>.Instance, () => FixedClock);
        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Confirmed, null),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyEditsAsync("doc-x", "TK-x", edits, Reviewer));
    }

    [Fact]
    public async Task ApplyEdits_UnknownFieldName_Throws()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);
        var edits = new Dictionary<string, FieldEdit>
        {
            ["notARealField"] = new(SchemaFieldStatus.Confirmed, null),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer));
    }

    [Fact]
    public async Task ApplyEdits_MissingSchemaEntry_Throws()
    {
        var entity = NewEntity(s => s.Remove("accusedName"));
        var (service, _) = BuildService(entity);
        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Confirmed, null),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer));
    }

    [Fact]
    public async Task ApplyEdits_TransitionToPending_Throws()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);
        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Pending, null),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer));
    }

    [Fact]
    public async Task ApplyEdits_ConfirmedWithDifferentReviewedValue_Throws()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);
        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Confirmed, "different-from-ocr"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer));
    }

    [Fact]
    public async Task ApplyEdits_CorrectedWithoutReviewedValue_Throws()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);
        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Corrected, null),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer));
    }

    [Fact]
    public async Task ApplyEdits_CorrectedEqualToOcrValue_Throws()
    {
        var entity = NewEntity();
        var (service, _) = BuildService(entity);
        var edits = new Dictionary<string, FieldEdit>
        {
            ["accusedName"] = new(SchemaFieldStatus.Corrected, "ocr"), // matches OcrValue
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyEditsAsync("doc-1", "TK-1", edits, Reviewer));
    }
}
