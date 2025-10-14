using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentOcrProcessor.Services;

public class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private const string SignaturePresent = "present";
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

            // Extract fields from the first document if available
            if (result.Documents.Count > 0)
            {
                var document = result.Documents[0];
                var fields = new Dictionary<string, object>();

                foreach (var field in document.Fields)
                {
                    var fieldName = field.Key;
                    var fieldValue = field.Value;

                    // Create a structured representation of each field
                    var fieldData = new Dictionary<string, object>
                    {
                        ["type"] = fieldValue.FieldType.ToString(),
                        ["confidence"] = fieldValue.Confidence
                    };

                    // Add the appropriate value based on field type
                    if (fieldValue.Content != null)
                    {
                        fieldData["content"] = fieldValue.Content;
                    }

                    // Add the typed value
                    switch (fieldValue.FieldType)
                    {
                        case Azure.AI.FormRecognizer.DocumentAnalysis.DocumentFieldType.String:
                            var stringValue = fieldValue.Value.AsString();
                            if (stringValue != null)
                                fieldData["valueString"] = stringValue;
                            break;
                        case Azure.AI.FormRecognizer.DocumentAnalysis.DocumentFieldType.Date:
                            try
                            {
                                var dateValue = fieldValue.Value.AsDate();
                                fieldData["valueDate"] = dateValue;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Failed to extract date value for field {FieldName}: {Message}", fieldName, ex.Message);
                            }
                            break;
                        case Azure.AI.FormRecognizer.DocumentAnalysis.DocumentFieldType.Time:
                            try
                            {
                                var timeValue = fieldValue.Value.AsTime();
                                fieldData["valueTime"] = timeValue;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Failed to extract time value for field {FieldName}: {Message}", fieldName, ex.Message);
                            }
                            break;
                        case Azure.AI.FormRecognizer.DocumentAnalysis.DocumentFieldType.PhoneNumber:
                            var phoneValue = fieldValue.Value.AsPhoneNumber();
                            if (phoneValue != null)
                                fieldData["valuePhoneNumber"] = phoneValue;
                            break;
                        case Azure.AI.FormRecognizer.DocumentAnalysis.DocumentFieldType.Double:
                            try
                            {
                                var doubleValue = fieldValue.Value.AsDouble();
                                fieldData["valueNumber"] = doubleValue;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Failed to extract double value for field {FieldName}: {Message}", fieldName, ex.Message);
                            }
                            break;
                        case Azure.AI.FormRecognizer.DocumentAnalysis.DocumentFieldType.Int64:
                            try
                            {
                                var int64Value = fieldValue.Value.AsInt64();
                                fieldData["valueInteger"] = int64Value;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Failed to extract integer value for field {FieldName}: {Message}", fieldName, ex.Message);
                            }
                            break;
                        case Azure.AI.FormRecognizer.DocumentAnalysis.DocumentFieldType.Signature:
                            // Signature fields indicate presence of a signature, not the actual signature data
                            fieldData["valueSignature"] = SignaturePresent;
                            break;
                    }

                    fields[fieldName] = fieldData;
                }

                extractedData["Fields"] = fields;
                _logger.LogInformation("Extracted {Count} fields from document", fields.Count);
            }
            else
            {
                _logger.LogWarning("No documents found in analysis result");
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
