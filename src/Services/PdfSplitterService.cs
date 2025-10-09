using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace DocumentOcrProcessor.Services;

public class PdfSplitterService : IPdfSplitterService
{
    private readonly ILogger<PdfSplitterService> _logger;
    private readonly IDocumentBoundaryDetectionStrategy _detectionStrategy;

    public PdfSplitterService(ILogger<PdfSplitterService> logger, IDocumentBoundaryDetectionStrategy detectionStrategy)
    {
        _logger = logger;
        _detectionStrategy = detectionStrategy;
    }

    public async Task<List<Stream>> SplitPdfIntoDocumentsAsync(Stream pdfStream, string ocrText)
    {
        _logger.LogInformation("Starting PDF split operation");
        var documents = new List<Stream>();

        try
        {
            pdfStream.Position = 0;
            using var inputDocument = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
            var totalPages = inputDocument.PageCount;
            _logger.LogInformation("PDF has {TotalPages} pages", totalPages);

            var documentBoundaries = await _aiFoundryService.DetectDocumentBoundariesAsync(ocrText, totalPages);
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
