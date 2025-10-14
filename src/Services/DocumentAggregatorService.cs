using DocumentOcrProcessor.Models;
using Microsoft.Extensions.Logging;

namespace DocumentOcrProcessor.Services;

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

        var documentGroups = new Dictionary<string, AggregatedDocument>();

        foreach (var pageResult in pageResults)
        {
            var identifier = ExtractIdentifier(pageResult, identifierFieldName);

            if (!documentGroups.ContainsKey(identifier))
            {
                documentGroups[identifier] = new AggregatedDocument
                {
                    Identifier = identifier
                };
            }

            documentGroups[identifier].Pages.Add(pageResult);
        }

        var aggregatedDocuments = documentGroups.Values.OrderBy(d => d.Pages.Min(p => p.PageNumber)).ToList();
        
        _logger.LogInformation("Aggregated into {DocumentCount} documents", aggregatedDocuments.Count);

        return aggregatedDocuments;
    }

    private string ExtractIdentifier(PageOcrResult pageResult, string identifierFieldName)
    {
        if (pageResult.ExtractedData.ContainsKey("Fields"))
        {
            var fields = pageResult.ExtractedData["Fields"] as Dictionary<string, object>;
            if (fields != null && fields.ContainsKey(identifierFieldName))
            {
                var fieldData = fields[identifierFieldName] as Dictionary<string, object>;
                if (fieldData != null && fieldData.ContainsKey("valueString"))
                {
                    return fieldData["valueString"]?.ToString() ?? $"page_{pageResult.PageNumber}";
                }
                if (fieldData != null && fieldData.ContainsKey("content"))
                {
                    return fieldData["content"]?.ToString() ?? $"page_{pageResult.PageNumber}";
                }
            }
        }

        _logger.LogWarning("No identifier found for page {PageNumber}, using page number as identifier", 
            pageResult.PageNumber);
        return $"page_{pageResult.PageNumber}";
    }
}
