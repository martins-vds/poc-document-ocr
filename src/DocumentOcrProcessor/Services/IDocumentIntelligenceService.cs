namespace DocumentOcrProcessor.Services;

public interface IDocumentIntelligenceService
{
    Task<Dictionary<string, object>> AnalyzeDocumentAsync(Stream documentStream);
}
