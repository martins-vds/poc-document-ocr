using DocumentOcr.Common.Models;
using DocumentOcr.Processor.Models;
using DocumentOcr.Processor.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentOcr.UnitTests.Services;

/// <summary>
/// T029 — DocumentAggregatorService forward-fill behavior (FR-020 / D2).
/// </summary>
public class DocumentAggregatorServiceTests
{
    private const string IdField = "fileTkNumber";

    private static DocumentAggregatorService NewService() =>
        new(NullLogger<DocumentAggregatorService>.Instance);

    private static PageOcrResult Page(int number, string? identifier)
    {
        var fields = new Dictionary<string, object>();
        if (identifier is not null)
        {
            fields[IdField] = new Dictionary<string, object> { ["valueString"] = identifier };
        }
        return new PageOcrResult
        {
            PageNumber = number,
            ExtractedData = new Dictionary<string, object> { ["Fields"] = fields },
        };
    }

    [Fact]
    public void Aggregate_PagesWithSameIdentifier_ProducesSingleGroup()
    {
        var pages = new List<PageOcrResult>
        {
            Page(1, "TK-1"),
            Page(2, "TK-1"),
            Page(3, "TK-1"),
        };

        var groups = NewService().AggregatePagesByIdentifier(pages, IdField);

        Assert.Single(groups);
        Assert.Equal("TK-1", groups[0].Identifier);
        Assert.Equal(3, groups[0].PageProvenance.Count);
        Assert.All(groups[0].PageProvenance, p => Assert.Equal(IdentifierSource.Extracted, p.IdentifierSource));
    }

    [Fact]
    public void Aggregate_GapsAreForwardFilled()
    {
        var pages = new List<PageOcrResult>
        {
            Page(1, "TK-1"),
            Page(2, null),
            Page(3, null),
            Page(4, "TK-2"),
            Page(5, null),
        };

        var groups = NewService().AggregatePagesByIdentifier(pages, IdField);

        Assert.Equal(2, groups.Count);
        Assert.Equal("TK-1", groups[0].Identifier);
        Assert.Equal(new[] { 1, 2, 3 }, groups[0].Pages.Select(p => p.PageNumber).ToArray());
        Assert.Equal(IdentifierSource.Extracted, groups[0].PageProvenance[0].IdentifierSource);
        Assert.Equal(IdentifierSource.Inferred, groups[0].PageProvenance[1].IdentifierSource);
        Assert.Equal(IdentifierSource.Inferred, groups[0].PageProvenance[2].IdentifierSource);

        Assert.Equal("TK-2", groups[1].Identifier);
        Assert.Equal(new[] { 4, 5 }, groups[1].Pages.Select(p => p.PageNumber).ToArray());
    }

    [Fact]
    public void Aggregate_LeadingPagesWithoutIdentifier_FormSyntheticGroup()
    {
        var pages = new List<PageOcrResult>
        {
            Page(1, null),
            Page(2, null),
            Page(3, "TK-1"),
        };

        var groups = NewService().AggregatePagesByIdentifier(pages, IdField);

        Assert.Equal(2, groups.Count);
        Assert.Equal(string.Empty, groups[0].Identifier);
        Assert.All(groups[0].PageProvenance, p => Assert.Equal(IdentifierSource.Inferred, p.IdentifierSource));
        Assert.Equal("TK-1", groups[1].Identifier);
    }

    [Fact]
    public void Aggregate_OutOfOrderInput_IsSortedByPageNumber()
    {
        var pages = new List<PageOcrResult>
        {
            Page(3, "TK-1"),
            Page(1, "TK-1"),
            Page(2, "TK-1"),
        };

        var groups = NewService().AggregatePagesByIdentifier(pages, IdField);

        Assert.Single(groups);
        Assert.Equal(new[] { 1, 2, 3 }, groups[0].Pages.Select(p => p.PageNumber).ToArray());
    }
}
