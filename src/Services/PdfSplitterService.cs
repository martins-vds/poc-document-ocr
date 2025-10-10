using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace DocumentOcrProcessor.Services;

public class PdfSplitterService : IPdfSplitterService
{
    private readonly ILogger<PdfSplitterService> _logger;
    private readonly AiBoundaryDetectionStrategy _aiBoundaryStrategy;
    private readonly ManualBoundaryDetectionStrategy _manualBoundaryStrategy;

    public PdfSplitterService(
        ILogger<PdfSplitterService> logger, 
        AiBoundaryDetectionStrategy aiBoundaryStrategy,
        ManualBoundaryDetectionStrategy manualBoundaryStrategy)
    {
        _logger = logger;
        _aiBoundaryStrategy = aiBoundaryStrategy;
        _manualBoundaryStrategy = manualBoundaryStrategy;
    }

    public async Task<List<Stream>> SplitPdfIntoDocumentsAsync(Stream pdfStream, string ocrText)
    {
        return await SplitPdfIntoDocumentsAsync(pdfStream, ocrText, useManualDetection: false);
    }

    public async Task<List<Stream>> SplitPdfIntoDocumentsAsync(Stream pdfStream, string ocrText, bool useManualDetection)
    {
        _logger.LogInformation("Starting PDF split operation");
        var documents = new List<Stream>();

        try
        {
            pdfStream.Position = 0;
            using var inputDocument = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
            var totalPages = inputDocument.PageCount;
            _logger.LogInformation("PDF has {TotalPages} pages", totalPages);

            // Select strategy based on configuration
            IDocumentBoundaryDetectionStrategy strategy = useManualDetection 
                ? _manualBoundaryStrategy 
                : _aiBoundaryStrategy;

            var documentBoundaries = await strategy.DetectDocumentBoundariesAsync(pdfStream, totalPages);
            _logger.LogInformation("Detected {Count} documents", documentBoundaries.Count);

            for (int i = 0; i < documentBoundaries.Count; i++)
            {
                var startPage = documentBoundaries[i];
                var endPage = (i < documentBoundaries.Count - 1) ? documentBoundaries[i + 1] - 1 : totalPages;

                var outputDocument = new PdfDocument();
                for (int pageIndex = startPage - 1; pageIndex < endPage; pageIndex++)
                {
                    outputDocument.AddPage(inputDocument.Pages[pageIndex]);
                }

                var documentStream = new MemoryStream();
                outputDocument.Save(documentStream, false);
                documentStream.Position = 0;
                documents.Add(documentStream);

                _logger.LogInformation("Created document {DocumentNumber} with pages {StartPage} to {EndPage}", 
                    i + 1, startPage, endPage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting PDF");
            throw;
        }

        return documents;
    }
}
