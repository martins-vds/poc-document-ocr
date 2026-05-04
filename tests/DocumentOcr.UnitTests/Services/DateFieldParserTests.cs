using DocumentOcr.Common.Services;

namespace DocumentOcr.UnitTests.Services;

public class DateFieldParserTests
{
    [Theory]
    [InlineData("1985JAN12", 1985, 1, 12)]
    [InlineData("2026MAY03", 2026, 5, 3)]
    [InlineData("  2000DEC31  ", 2000, 12, 31)]
    [InlineData("1999feb05", 1999, 2, 5)]
    public void TryParse_CompactForm_ReturnsExpectedDate(string raw, int y, int m, int d)
    {
        Assert.True(DateFieldParser.TryParse(raw, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
    }

    [Theory]
    [InlineData("3rd day of January, 2026", 2026, 1, 3)]
    [InlineData("1st DAY OF MARCH, 1990", 1990, 3, 1)]
    [InlineData("22ND DAY OF DECEMBER 2010", 2010, 12, 22)]
    [InlineData(" 7  TH  DAY  OF  JULY ,  1976 ", 1976, 7, 7)]
    public void TryParse_LongForm_ReturnsExpectedDate(string raw, int y, int m, int d)
    {
        Assert.True(DateFieldParser.TryParse(raw, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    [InlineData("2026FEB30")]                       // calendar invalid
    [InlineData("31st DAY OF FEBRUARY, 2026")]      // calendar invalid
    [InlineData("2026XYZ12")]                        // unknown month
    [InlineData("2026-05-03")]                       // ISO format not supported by parser
    public void TryParse_InvalidInputs_ReturnsFalse(string? raw)
    {
        Assert.False(DateFieldParser.TryParse(raw, out var date));
        Assert.Equal(default, date);
    }
}
