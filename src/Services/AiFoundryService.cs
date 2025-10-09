using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentOcrProcessor.Services;

public class AiFoundryService : IAiFoundryService
{
    private readonly ILogger<AiFoundryService> _logger;
    private readonly ChatCompletionsClient _client;

    public AiFoundryService(ILogger<AiFoundryService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var endpoint = configuration["AzureAiFoundry:Endpoint"];
        var apiKey = configuration["AzureAiFoundry:ApiKey"];

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Azure AI Foundry configuration is missing");
        }

        _client = new ChatCompletionsClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<List<int>> DetectDocumentBoundariesAsync(string ocrText, int totalPages)
    {
        _logger.LogInformation("Detecting document boundaries for PDF with {TotalPages} pages using OCR text", totalPages);

        try
        {
            var systemPrompt = @"You are an AI assistant that analyzes OCR-extracted text from PDF documents to detect logical document boundaries. 
A single PDF may contain multiple independent documents. Each document may have one or more pages.
Your task is to identify the starting page number of each document in the PDF based on the OCR text.
Return only a comma-separated list of page numbers (starting from 1) where each document begins.
For example, if there are 3 documents starting at pages 1, 5, and 10, return: 1,5,10";

            var userPrompt = $@"Analyze the following OCR text extracted from a PDF with {totalPages} pages and identify where each logical document begins.
Consider factors like:
- Title pages or headers indicating a new document
- Consistent formatting within a document
- Changes in content type or structure
- Page breaks that suggest document boundaries

OCR Text:
{ocrText}

Return the starting page numbers as a comma-separated list.";

            var requestOptions = new ChatCompletionsOptions
            {
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                },
                MaxTokens = 1000,
                Temperature = 0.3f
            };

            var response = await _client.CompleteAsync(requestOptions);
            var content = response.Value.Content;

            _logger.LogInformation("AI Foundry response: {Response}", content);

            var boundaries = ParseBoundaries(content, totalPages);
            return boundaries;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting document boundaries, falling back to single document");
            return new List<int> { 1 };
        }
    }

    private List<int> ParseBoundaries(string content, int totalPages)
    {
        var boundaries = new List<int>();
        
        var parts = content.Split(new[] { ',', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim(), out int pageNumber) && pageNumber > 0 && pageNumber <= totalPages)
            {
                boundaries.Add(pageNumber);
            }
        }

        if (boundaries.Count == 0 || boundaries[0] != 1)
        {
            boundaries.Insert(0, 1);
        }

        boundaries.Sort();
        return boundaries.Distinct().ToList();
    }
}
