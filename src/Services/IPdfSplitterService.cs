namespace DocumentOcrProcessor.Services;

public interface IPdfSplitterService
{
    Task<List<Stream>> SplitPdfIntoDocumentsAsync(Stream pdfStream, List<int>? manualBoundaries = null);
}
