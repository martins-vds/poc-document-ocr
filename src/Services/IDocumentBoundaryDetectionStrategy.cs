namespace DocumentOcrProcessor.Services;

public interface IDocumentBoundaryDetectionStrategy
{
    Task<List<int>> DetectDocumentBoundariesAsync(Stream pdfStream, int totalPages);
}
