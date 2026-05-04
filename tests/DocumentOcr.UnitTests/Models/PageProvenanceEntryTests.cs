using DocumentOcr.Common.Models;

namespace DocumentOcr.UnitTests.Models;

/// <summary>
/// T006 — invariants for <see cref="PageProvenanceEntry"/> (FR-020).
/// </summary>
public class PageProvenanceEntryTests
{
    [Fact]
    public void Inferred_HasNullExtractedIdentifier()
    {
        var entry = PageProvenanceEntry.Inferred(2);

        Assert.Equal(IdentifierSource.Inferred, entry.IdentifierSource);
        Assert.Null(entry.ExtractedIdentifier);
    }

    [Fact]
    public void Extracted_RequiresNonNullIdentifier()
    {
        Assert.Throws<ArgumentException>(() => PageProvenanceEntry.Extracted(1, ""));
        Assert.Throws<ArgumentException>(() => PageProvenanceEntry.Extracted(1, null!));
    }

    [Fact]
    public void Extracted_HasNonNullIdentifier()
    {
        var entry = PageProvenanceEntry.Extracted(1, "TK-2026-00417");

        Assert.Equal(IdentifierSource.Extracted, entry.IdentifierSource);
        Assert.Equal("TK-2026-00417", entry.ExtractedIdentifier);
    }

    [Fact]
    public void Validate_InferredWithExtractedIdentifier_Throws()
    {
        var entry = new PageProvenanceEntry
        {
            PageNumber = 1,
            IdentifierSource = IdentifierSource.Inferred,
            ExtractedIdentifier = "should-be-null",
        };

        Assert.Throws<InvalidOperationException>(() => entry.EnsureValid());
    }

    [Fact]
    public void Validate_ExtractedWithoutIdentifier_Throws()
    {
        var entry = new PageProvenanceEntry
        {
            PageNumber = 1,
            IdentifierSource = IdentifierSource.Extracted,
            ExtractedIdentifier = null,
        };

        Assert.Throws<InvalidOperationException>(() => entry.EnsureValid());
    }
}
