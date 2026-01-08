namespace DocumentOcr.Processor.Services;

public interface IDocumentIntelligenceService
{
    Task<Dictionary<string, object>> AnalyzeDocumentAsync(Stream documentStream);
}
