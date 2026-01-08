using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;

namespace DocumentOcr.Processor.Services;

public class PdfToImageService : IPdfToImageService
{
    private readonly ILogger<PdfToImageService> _logger;

    public PdfToImageService(ILogger<PdfToImageService> logger)
    {
        _logger = logger;
    }

    public async Task<List<Stream>> ConvertPdfPagesToImagesAsync(Stream pdfStream)
    {
        _logger.LogInformation("Converting PDF pages to images");
        var imageStreams = new List<Stream>();

        try
        {
            pdfStream.Position = 0;
            var pageCount = Conversion.GetPageCount(pdfStream, leaveOpen: true);
            _logger.LogInformation("PDF has {PageCount} pages", pageCount);

            for (int i = 0; i < pageCount; i++)
            {
                pdfStream.Position = 0;
                using var image = Conversion.ToImage(pdfStream, page: i, leaveOpen: true);
                
                var imageStream = new MemoryStream();
                image.Encode(imageStream, SKEncodedImageFormat.Png, 100);
                imageStream.Position = 0;
                imageStreams.Add(imageStream);
                
                _logger.LogInformation("Converted page {PageNumber} to image", i + 1);
            }

            return await Task.FromResult(imageStreams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting PDF pages to images");
            throw;
        }
    }
}
