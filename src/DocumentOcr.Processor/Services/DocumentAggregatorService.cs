using DocumentOcr.Common.Models;
using DocumentOcr.Processor.Models;
using Microsoft.Extensions.Logging;

namespace DocumentOcr.Processor.Services;

/// <summary>
/// T031 — Linear forward-fill aggregation per research.md D2.
///
/// Pages are processed in PageNumber order. When a page extracts an identifier
/// it starts (or continues) a group. Pages without an extracted identifier
/// inherit the most recently seen identifier (forward-fill) and are recorded
/// with <see cref="IdentifierSource.Inferred"/>. Pages preceding the first
/// extracted identifier form an "unknown" group whose provenance entries are
/// all Inferred.
/// </summary>
public class DocumentAggregatorService : IDocumentAggregatorService
{
    private readonly ILogger<DocumentAggregatorService> _logger;

    public DocumentAggregatorService(ILogger<DocumentAggregatorService> logger)
    {
        _logger = logger;
    }

    public List<AggregatedDocument> AggregatePagesByIdentifier(List<PageOcrResult> pageResults, string identifierFieldName)
    {
        _logger.LogInformation("Aggregating {PageCount} pages by identifier field: {IdentifierFieldName}",
            pageResults.Count, identifierFieldName);

        var ordered = pageResults.OrderBy(p => p.PageNumber).ToList();

        var groups = new List<AggregatedDocument>();
        AggregatedDocument? current = null;
        string? currentIdentifier = null;

        foreach (var page in ordered)
        {
            var extracted = TryExtractIdentifier(page, identifierFieldName);

            if (extracted is not null)
            {
                if (currentIdentifier is null || !string.Equals(extracted, currentIdentifier, StringComparison.Ordinal))
                {
                    current = new AggregatedDocument { Identifier = extracted };
                    groups.Add(current);
                    currentIdentifier = extracted;
                }

                current!.Pages.Add(page);
                current.PageProvenance.Add(PageProvenanceEntry.Extracted(page.PageNumber, extracted));
            }
            else if (current is not null)
            {
                current.Pages.Add(page);
                current.PageProvenance.Add(PageProvenanceEntry.Inferred(page.PageNumber));
                _logger.LogWarning(
                    "FR-020 - page {PageNumber} had no extracted identifier; forward-filling from '{Identifier}'.",
                    page.PageNumber, currentIdentifier);
            }
            else
            {
                current = new AggregatedDocument { Identifier = string.Empty };
                groups.Add(current);
                currentIdentifier = null;
                current.Pages.Add(page);
                current.PageProvenance.Add(PageProvenanceEntry.Inferred(page.PageNumber));
                _logger.LogWarning(
                    "FR-020 - page {PageNumber} appears before any extracted identifier; assigning to synthetic group.",
                    page.PageNumber);
            }
        }

        _logger.LogInformation("Aggregated into {DocumentCount} documents", groups.Count);
        return groups;
    }

    private static string? TryExtractIdentifier(PageOcrResult pageResult, string identifierFieldName)
    {
        if (!pageResult.ExtractedData.TryGetValue("Fields", out var fieldsObj) ||
            fieldsObj is not Dictionary<string, object> fields)
        {
            return null;
        }
        if (!fields.TryGetValue(identifierFieldName, out var fieldObj) ||
            fieldObj is not Dictionary<string, object> fieldData)
        {
            return null;
        }

        if (fieldData.TryGetValue("valueString", out var s) && s is string ss && !string.IsNullOrWhiteSpace(ss))
        {
            return ss;
        }
        if (fieldData.TryGetValue("content", out var c) && c is string cs && !string.IsNullOrWhiteSpace(cs))
        {
            return cs;
        }
        return null;
    }
}
