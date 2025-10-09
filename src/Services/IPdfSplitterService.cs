namespace DocumentOcrProcessor.Services;

public interface IPdfSplitterService
{
    Task<List<Stream>> SplitPdfIntoDocumentsAsync(Stream pdfStream);
}
