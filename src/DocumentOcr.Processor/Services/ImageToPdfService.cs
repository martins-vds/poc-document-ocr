using DocumentOcr.Processor.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace DocumentOcr.Processor.Services;

public class ImageToPdfService : IImageToPdfService
{
    private readonly ILogger<ImageToPdfService> _logger;

    public ImageToPdfService(ILogger<ImageToPdfService> logger)
    {
        _logger = logger;
    }

    public async Task<Stream> CreatePdfFromImagesAsync(List<PageOcrResult> pages)
    {
        _logger.LogInformation("Creating PDF from {PageCount} images", pages.Count);

        try
        {
            var pdfStream = new MemoryStream();

            using (var document = SKDocument.CreatePdf(pdfStream))
            {
                foreach (var page in pages.OrderBy(p => p.PageNumber))
                {
                    page.ImageStream.Position = 0;

                    using var image = SKImage.FromEncodedData(page.ImageStream);
                    if (image == null)
                    {
                        _logger.LogWarning("Failed to decode image for page {PageNumber}", page.PageNumber);
                        continue;
                    }

                    using var canvas = document.BeginPage(image.Width, image.Height);
                    canvas.DrawImage(image, 0, 0);
                    document.EndPage();

                    _logger.LogInformation("Added page {PageNumber} to PDF", page.PageNumber);
                }

                document.Close();
            }

            pdfStream.Position = 0;

            _logger.LogInformation("Successfully created PDF with {PageCount} pages", pages.Count);
            return await Task.FromResult(pdfStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PDF from images");
            throw;
        }
    }
}
