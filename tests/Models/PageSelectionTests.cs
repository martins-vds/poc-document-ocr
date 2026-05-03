using DocumentOcr.Common.Models;

namespace DocumentOcr.Tests.Models;

public class PageSelectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public void TryParse_NullOrWhitespace_YieldsAllPages(string? input)
    {
        var ok = PageSelection.TryParse(input, maxPage: null, out var result, out var err);

        Assert.True(ok);
        Assert.Null(err);
        Assert.True(result.IsAllPages);
        Assert.Equal(string.Empty, result.Expression);
        Assert.Empty(result.Pages);
    }

    [Fact]
    public void TryParse_WhitespaceTolerant_ParsesNormalized()
    {
        var ok = PageSelection.TryParse("3 - 12 ,  15", maxPage: null, out var result, out var err);

        Assert.True(ok);
        Assert.Null(err);
        Assert.False(result.IsAllPages);
        Assert.Equal("3-12, 15", result.Expression);
        Assert.Equal(new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 15 }, result.Pages);
    }

    [Fact]
    public void TryParse_OverlappingTokens_AreDeduplicatedAndSorted()
    {
        var ok = PageSelection.TryParse("3-7, 5-10", maxPage: null, out var result, out _);

        Assert.True(ok);
        Assert.Equal(new[] { 3, 4, 5, 6, 7, 8, 9, 10 }, result.Pages);
        Assert.Equal(8, result.Pages.Count);
    }

    [Fact]
    public void TryParse_ReversedBounds_Rejected()
    {
        var ok = PageSelection.TryParse("5-3", maxPage: null, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("5-3", err);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0-5")]
    public void TryParse_ZeroOrNegativePages_Rejected(string input)
    {
        var ok = PageSelection.TryParse(input, maxPage: null, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("1 or greater", err);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1-")]
    [InlineData("-5")]
    [InlineData(",,,")]
    [InlineData("1,,2")]
    [InlineData("1-2-3")]
    public void TryParse_MalformedTokens_Rejected(string input)
    {
        var ok = PageSelection.TryParse(input, maxPage: null, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void TryParse_MaxPage_RejectsOutOfBoundsToken()
    {
        var ok = PageSelection.TryParse("25", maxPage: 20, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("25", err);
        Assert.Contains("20", err);
    }

    [Fact]
    public void TryParse_MaxPage_RejectsOutOfBoundsRangeEnd()
    {
        var ok = PageSelection.TryParse("3-25", maxPage: 20, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("25", err);
        Assert.Contains("20", err);
    }

    [Fact]
    public void TryParse_MaxPage_AcceptsInBoundsExpression()
    {
        var ok = PageSelection.TryParse("3-12, 15", maxPage: 20, out var result, out var err);

        Assert.True(ok);
        Assert.Null(err);
        Assert.Equal(11, result.Pages.Count);
    }

    [Fact]
    public void Resolve_AllPages_Returns1ToN()
    {
        var pages = PageSelection.All.Resolve(5);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, pages);
    }

    [Fact]
    public void Resolve_AllPages_OnZeroTotalPages_ReturnsEmpty()
    {
        Assert.Empty(PageSelection.All.Resolve(0));
    }

    [Fact]
    public void Resolve_ExplicitSelection_ReturnsPages()
    {
        PageSelection.TryParse("3-5, 8", maxPage: null, out var sel, out _);

        Assert.Equal(new[] { 3, 4, 5, 8 }, sel.Resolve(10));
    }

    [Fact]
    public void Resolve_ExplicitSelection_ThrowsWhenPageExceedsTotal()
    {
        PageSelection.TryParse("3-5, 25", maxPage: null, out var sel, out _);

        var ex = Assert.Throws<InvalidOperationException>(() => sel.Resolve(20));
        Assert.Contains("25", ex.Message);
        Assert.Contains("20", ex.Message);
    }

    [Fact]
    public void All_IsSingleton()
    {
        Assert.Same(PageSelection.All, PageSelection.All);
        Assert.True(PageSelection.All.IsAllPages);
    }
}
