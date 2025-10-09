namespace DocumentOcrProcessor.Services;

public interface IAiFoundryService
{
    Task<List<int>> DetectDocumentBoundariesAsync(string ocrText, int totalPages);
}
