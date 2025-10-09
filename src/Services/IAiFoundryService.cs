namespace DocumentOcrProcessor.Services;

public interface IAiFoundryService
{
    Task<List<int>> DetectDocumentBoundariesAsync(Stream pdfStream, int totalPages);
}
