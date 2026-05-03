using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace DocumentOcr.Common.Models;

/// <summary>
/// Print-dialog–style selection of 1-indexed pages within a PDF.
/// Shared by the WebApp (validation), the Operations API HTTP entry point
/// (validation + persistence), and the queue worker (consumption).
/// See specs/002-upload-page-range-selection/data-model.md §1.
/// </summary>
public sealed class PageSelection
{
    /// <summary>Sentinel meaning "all pages".</summary>
    public static PageSelection All { get; } = new(string.Empty, Array.Empty<int>());

    /// <summary>Original (whitespace-normalized) user expression. Empty when "all pages".</summary>
    [JsonProperty("expression")]
    public string Expression { get; }

    /// <summary>Sorted, deduplicated, 1-indexed page numbers. Empty when "all pages".</summary>
    [JsonProperty("pages")]
    public IReadOnlyList<int> Pages { get; }

    /// <summary>True when this represents the implicit "all pages" default.</summary>
    [JsonIgnore]
    public bool IsAllPages => Expression.Length == 0;

    [JsonConstructor]
    private PageSelection(string expression, IReadOnlyList<int> pages)
    {
        Expression = expression ?? string.Empty;
        Pages = pages ?? Array.Empty<int>();
    }

    /// <summary>
    /// Parses a print-dialog–style expression. Returns true on success (including
    /// for null/whitespace input, which yields <see cref="All"/>). On failure,
    /// <paramref name="result"/> is <see cref="All"/> and <paramref name="error"/>
    /// carries a human-readable message.
    /// </summary>
    /// <param name="input">User expression; null or whitespace ⇒ "all pages".</param>
    /// <param name="maxPage">Optional upper bound; when supplied, every page must be ≤ <paramref name="maxPage"/>.</param>
    public static bool TryParse(string? input, int? maxPage, out PageSelection result, out string? error)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            result = All;
            error = null;
            return true;
        }

        var pages = new SortedSet<int>();
        var tokens = input.Split(',', StringSplitOptions.None);

        foreach (var rawToken in tokens)
        {
            var token = rawToken.Trim();
            if (token.Length == 0)
            {
                result = All;
                error = $"Invalid token '{rawToken}': use page numbers or N-M ranges.";
                return false;
            }

            var dashIndex = token.IndexOf('-');
            if (dashIndex < 0)
            {
                if (!TryParsePage(token, out var page))
                {
                    result = All;
                    error = $"Invalid token '{token}': use page numbers or N-M ranges.";
                    return false;
                }
                if (page < 1)
                {
                    result = All;
                    error = "Page numbers must be 1 or greater.";
                    return false;
                }
                if (maxPage is int max && page > max)
                {
                    result = All;
                    error = $"Page {page} exceeds document length ({max}).";
                    return false;
                }
                pages.Add(page);
            }
            else
            {
                var startPart = token.Substring(0, dashIndex).Trim();
                var endPart = token.Substring(dashIndex + 1).Trim();
                if (!TryParsePage(startPart, out var start) || !TryParsePage(endPart, out var end))
                {
                    result = All;
                    error = $"Invalid token '{token}': use page numbers or N-M ranges.";
                    return false;
                }
                if (start < 1 || end < 1)
                {
                    result = All;
                    error = "Page numbers must be 1 or greater.";
                    return false;
                }
                if (start > end)
                {
                    result = All;
                    error = $"Range '{start}-{end}' has start greater than end.";
                    return false;
                }
                if (maxPage is int max && end > max)
                {
                    result = All;
                    error = $"Page {end} exceeds document length ({max}).";
                    return false;
                }
                for (var p = start; p <= end; p++)
                {
                    pages.Add(p);
                }
            }
        }

        if (pages.Count == 0)
        {
            // Defensive — unreachable given the rules above.
            result = All;
            error = "Page selection resolved to zero pages.";
            return false;
        }

        result = new PageSelection(NormalizeExpression(input), pages.ToArray());
        error = null;
        return true;
    }

    /// <summary>
    /// Returns the absolute page list against the actual page count.
    /// For <see cref="IsAllPages"/>, returns 1..<paramref name="totalPages"/>.
    /// For an explicit selection, returns <see cref="Pages"/> but throws
    /// <see cref="InvalidOperationException"/> if any page exceeds <paramref name="totalPages"/>.
    /// </summary>
    public IReadOnlyList<int> Resolve(int totalPages)
    {
        if (totalPages < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPages));
        }

        if (IsAllPages)
        {
            var all = new int[totalPages];
            for (var i = 0; i < totalPages; i++)
            {
                all[i] = i + 1;
            }
            return all;
        }

        var max = Pages.Count > 0 ? Pages[Pages.Count - 1] : 0;
        if (max > totalPages)
        {
            throw new InvalidOperationException(
                $"Page {max} exceeds document length ({totalPages}).");
        }
        return Pages;
    }

    private static bool TryParsePage(string text, out int page) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out page);

    private static string NormalizeExpression(string input)
    {
        // Strip surrounding whitespace from each token so the persisted
        // expression looks tidy; preserve the user's choice of separators.
        var sb = new StringBuilder(input.Length);
        var tokens = input.Split(',');
        for (var i = 0; i < tokens.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            var token = tokens[i].Trim();
            var dash = token.IndexOf('-');
            if (dash < 0)
            {
                sb.Append(token);
            }
            else
            {
                sb.Append(token.Substring(0, dash).Trim());
                sb.Append('-');
                sb.Append(token.Substring(dash + 1).Trim());
            }
        }
        return sb.ToString();
    }
}
