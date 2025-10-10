namespace DocumentOcrProcessor.Services;

public interface IPdfSplitterService
{
    Task<List<Stream>> SplitPdfIntoDocumentsAsync(Stream pdfStream, string ocrText);
    Task<List<Stream>> SplitPdfIntoDocumentsAsync(Stream pdfStream, string ocrText, bool useManualDetection);
}
