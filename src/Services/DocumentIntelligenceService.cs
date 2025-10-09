using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentOcrProcessor.Services;

public class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly ILogger<DocumentIntelligenceService> _logger;
    private readonly DocumentAnalysisClient _client;

    public DocumentIntelligenceService(ILogger<DocumentIntelligenceService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var endpoint = configuration["DocumentIntelligence:Endpoint"];
        var apiKey = configuration["DocumentIntelligence:ApiKey"];

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Document Intelligence configuration is missing");
        }

        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<Dictionary<string, object>> AnalyzeDocumentAsync(Stream documentStream)
    {
        _logger.LogInformation("Analyzing document with Document Intelligence");
        var extractedData = new Dictionary<string, object>();

        try
        {
            documentStream.Position = 0;
            var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-document", documentStream);
            var result = operation.Value;

            _logger.LogInformation("Document analysis completed. Found {PageCount} pages", result.Pages.Count);

            extractedData["PageCount"] = result.Pages.Count;
            extractedData["Content"] = result.Content;

            if (result.KeyValuePairs.Count > 0)
            {
                var keyValuePairs = new Dictionary<string, string>();
                foreach (var kvp in result.KeyValuePairs)
                {
                    var key = kvp.Key.Content ?? "Unknown";
                    var value = kvp.Value?.Content ?? string.Empty;
                    keyValuePairs[key] = value;
                }
                extractedData["KeyValuePairs"] = keyValuePairs;
                _logger.LogInformation("Extracted {Count} key-value pairs", keyValuePairs.Count);
            }

            if (result.Tables.Count > 0)
            {
                var tables = new List<object>();
                foreach (var table in result.Tables)
                {
                    var tableData = new
                    {
                        RowCount = table.RowCount,
                        ColumnCount = table.ColumnCount,
                        Cells = table.Cells.Select(c => new
                        {
                            c.RowIndex,
                            c.ColumnIndex,
                            c.Content
                        }).ToList()
                    };
                    tables.Add(tableData);
                }
                extractedData["Tables"] = tables;
                _logger.LogInformation("Extracted {Count} tables", tables.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document");
            throw;
        }

        return extractedData;
    }
}
