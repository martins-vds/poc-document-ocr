using DocumentOcr.Common.Models;
using DocumentOcr.Processor.Models;
using DocumentOcr.Processor.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentOcr.Tests.Services;

/// <summary>
/// T020 — covers contract guarantees from
/// contracts/IDocumentSchemaMapperService.md (1, 4, 5, 6, 7).
/// </summary>
public class DocumentSchemaMapperServiceTests
{
    private static readonly DateTime FixedClock = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FixedId = new("11111111-1111-1111-1111-111111111111");

    private static DocumentSchemaMapperService NewMapper() =>
        new(NullLogger<DocumentSchemaMapperService>.Instance, () => FixedClock, () => FixedId);

    private static PageOcrResult Page(int number, Dictionary<string, object>? fields = null) =>
        new()
        {
            PageNumber = number,
            ExtractedData = new Dictionary<string, object>
            {
                ["Fields"] = fields ?? new Dictionary<string, object>(),
            },
        };

    private static Dictionary<string, object> Field(string value, double confidence) =>
        new()
        {
            ["valueString"] = value,
            ["confidence"] = confidence,
        };

    private static Dictionary<string, object> SignatureField(string value, double confidence) =>
        new()
        {
            ["valueSignature"] = value,
            ["confidence"] = confidence,
        };

    [Fact]
    public void Map_AlwaysPopulatesAll13SchemaKeys()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument
        {
            Identifier = "TK-1",
            Pages = { Page(1, new Dictionary<string, object> { ["fileTkNumber"] = Field("TK-1", 0.99) }) },
        };

        var entity = mapper.Map(doc, 1, "input.pdf", "https://blob/url", "out.pdf");

        Assert.Equal(13, entity.Schema.Count);
        foreach (var name in ProcessedDocumentSchema.FieldNames)
        {
            Assert.True(entity.Schema.ContainsKey(name), $"Missing schema key '{name}'");
            Assert.Equal(SchemaFieldStatus.Pending, entity.Schema[name].FieldStatus);
        }
    }

    [Fact]
    public void Map_AbsentFieldsAreNullPending()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument { Identifier = "TK-1", Pages = { Page(1) } };

        var entity = mapper.Map(doc, 1, "input.pdf", "url", "out.pdf");

        var absent = entity.Schema["accusedName"];
        Assert.Null(absent.OcrValue);
        Assert.Null(absent.OcrConfidence);
        Assert.Equal(SchemaFieldStatus.Pending, absent.FieldStatus);
    }

    [Fact]
    public void Map_BlankIdentifier_UsesSyntheticFallback()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument { Identifier = "", Pages = { Page(7) } };

        var entity = mapper.Map(doc, 1, "scan.pdf", "url", "out.pdf");

        Assert.Equal("unknown-scan.pdf-7", entity.Identifier);
    }

    [Fact]
    public void Map_PreservesPageProvenance()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument
        {
            Identifier = "TK-1",
            Pages = { Page(1), Page(2) },
            PageProvenance =
            {
                PageProvenanceEntry.Extracted(1, "TK-1"),
                PageProvenanceEntry.Inferred(2),
            },
        };

        var entity = mapper.Map(doc, 1, "input.pdf", "url", "out.pdf");

        Assert.Equal(2, entity.PageProvenance.Count);
        Assert.Equal(IdentifierSource.Extracted, entity.PageProvenance[0].IdentifierSource);
        Assert.Equal(IdentifierSource.Inferred, entity.PageProvenance[1].IdentifierSource);
    }

    [Fact]
    public void Map_HighestConfidencePageWins_ForSingleValueFields()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument
        {
            Identifier = "TK-1",
            Pages =
            {
                Page(1, new Dictionary<string, object> { ["accusedName"] = Field("Alice", 0.60) }),
                Page(2, new Dictionary<string, object> { ["accusedName"] = Field("Alyce", 0.95) }),
                Page(3, new Dictionary<string, object> { ["accusedName"] = Field("Alise", 0.80) }),
            },
        };

        var entity = mapper.Map(doc, 1, "input.pdf", "url", "out.pdf");

        Assert.Equal("Alyce", entity.Schema["accusedName"].OcrValue);
        Assert.Equal(0.95, entity.Schema["accusedName"].OcrConfidence);
    }

    [Fact]
    public void Map_MultiValueFields_ConcatenatedInPageOrder_WithMinConfidence()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument
        {
            Identifier = "TK-1",
            Pages =
            {
                Page(2, new Dictionary<string, object> { ["mainCharge"] = Field("Charge B", 0.80) }),
                Page(1, new Dictionary<string, object> { ["mainCharge"] = Field("Charge A", 0.95) }),
            },
        };

        var entity = mapper.Map(doc, 1, "input.pdf", "url", "out.pdf");

        Assert.Equal("Charge A\nCharge B", entity.Schema["mainCharge"].OcrValue);
        Assert.Equal(0.80, entity.Schema["mainCharge"].OcrConfidence);
    }

    [Fact]
    public void Map_SignatureField_PresentMapsToTrue()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument
        {
            Identifier = "TK-1",
            Pages = { Page(1, new Dictionary<string, object> { ["judgeSignature"] = SignatureField("present", 0.90) }) },
        };

        var entity = mapper.Map(doc, 1, "input.pdf", "url", "out.pdf");

        Assert.Equal(true, entity.Schema["judgeSignature"].OcrValue);
    }

    [Fact]
    public void Map_SignatureField_AbsentMapsToFalse()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument { Identifier = "TK-1", Pages = { Page(1) } };

        var entity = mapper.Map(doc, 1, "input.pdf", "url", "out.pdf");

        Assert.Null(entity.Schema["judgeSignature"].OcrValue);
        Assert.Equal(SchemaFieldStatus.Pending, entity.Schema["judgeSignature"].FieldStatus);
    }

    [Fact]
    public void Map_InitialState_AllFieldsPending_DocumentReviewPending()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument
        {
            Identifier = "TK-1",
            Pages = { Page(1, new Dictionary<string, object> { ["fileTkNumber"] = Field("TK-1", 0.99) }) },
        };

        var entity = mapper.Map(doc, 5, "input.pdf", "https://blob/url", "out.pdf");

        Assert.Equal(ReviewStatus.Pending, entity.ReviewStatus);
        Assert.Null(entity.ReviewedBy);
        Assert.Null(entity.ReviewedAt);
        Assert.Null(entity.CheckedOutBy);
        Assert.Null(entity.CheckedOutAt);
        Assert.Null(entity.LastCheckedInBy);
        Assert.Null(entity.LastCheckedInAt);
        Assert.Equal(FixedClock, entity.ProcessedAt);
        Assert.Equal(5, entity.DocumentNumber);
        Assert.Equal(1, entity.PageCount);
        Assert.Equal(new[] { 1 }, entity.PageNumbers);
    }

    [Fact]
    public void Map_ZeroPages_Throws()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument { Identifier = "TK-1" };

        Assert.Throws<ArgumentException>(() => mapper.Map(doc, 1, "input.pdf", "url", "out.pdf"));
    }

    [Fact]
    public void Map_SortsPagesByNumber_ForPageNumbersList()
    {
        var mapper = NewMapper();
        var doc = new AggregatedDocument
        {
            Identifier = "TK-1",
            Pages = { Page(3), Page(1), Page(2) },
        };

        var entity = mapper.Map(doc, 1, "input.pdf", "url", "out.pdf");

        Assert.Equal(new[] { 1, 2, 3 }, entity.PageNumbers);
    }
}
