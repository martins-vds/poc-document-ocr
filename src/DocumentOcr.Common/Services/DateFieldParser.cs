using System.Globalization;
using System.Text.RegularExpressions;

namespace DocumentOcr.Common.Services;

/// <summary>
/// Parses the two date string patterns the upstream Document Intelligence
/// model returns for date fields (<c>accusedDateOfBirth</c>, <c>signedOn</c>,
/// <c>endorsementSignedOn</c>):
///
/// <list type="bullet">
/// <item>Compact form: <c>YYYYMMMDD</c> e.g. <c>1985JAN12</c>.</item>
/// <item>Long form:    <c>Nth DAY OF MONTH, YYYY</c> e.g. <c>3rd day of January, 2026</c>.</item>
/// </list>
///
/// Returns <c>null</c> when the input doesn't match either pattern OR when
/// the parsed date is invalid (e.g. February 30). Casing is ignored.
/// </summary>
public static class DateFieldParser
{
    private static readonly Regex CompactRegex = new(
        @"^\s*(?<YEAR>\d{4})(?<MON>JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)(?<DAY>\d{1,2})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static readonly Regex LongRegex = new(
        @"^\s*(?<DAY>\d{1,2})\s*(?:ST|ND|RD|TH)?\s*DAY\s*OF\s*(?<MONTH>JANUARY|FEBRUARY|MARCH|APRIL|MAY|JUNE|JULY|AUGUST|SEPTEMBER|OCTOBER|NOVEMBER|DECEMBER)\s*,?\s*(?<YEAR>\d{4})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static readonly Dictionary<string, int> ShortMonths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JAN"] = 1,
        ["FEB"] = 2,
        ["MAR"] = 3,
        ["APR"] = 4,
        ["MAY"] = 5,
        ["JUN"] = 6,
        ["JUL"] = 7,
        ["AUG"] = 8,
        ["SEP"] = 9,
        ["OCT"] = 10,
        ["NOV"] = 11,
        ["DEC"] = 12,
    };

    /// <summary>
    /// Try to parse <paramref name="raw"/> into a <see cref="DateOnly"/>.
    /// Returns <c>false</c> for null/empty input or anything that doesn't
    /// match the two supported patterns or fails calendar validation.
    /// </summary>
    public static bool TryParse(string? raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var compact = CompactRegex.Match(raw);
        if (compact.Success)
        {
            var year = int.Parse(compact.Groups["YEAR"].Value, CultureInfo.InvariantCulture);
            var month = ShortMonths[compact.Groups["MON"].Value];
            var day = int.Parse(compact.Groups["DAY"].Value, CultureInfo.InvariantCulture);
            return TryBuild(year, month, day, out date);
        }

        var longMatch = LongRegex.Match(raw);
        if (longMatch.Success)
        {
            var year = int.Parse(longMatch.Groups["YEAR"].Value, CultureInfo.InvariantCulture);
            var monthName = longMatch.Groups["MONTH"].Value;
            var day = int.Parse(longMatch.Groups["DAY"].Value, CultureInfo.InvariantCulture);
            if (!DateTime.TryParseExact(
                    monthName,
                    "MMMM",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var monthDate))
            {
                return false;
            }
            return TryBuild(year, monthDate.Month, day, out date);
        }

        return false;
    }

    private static bool TryBuild(int year, int month, int day, out DateOnly date)
    {
        date = default;
        if (month < 1 || month > 12) return false;
        if (day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
        date = new DateOnly(year, month, day);
        return true;
    }
}
