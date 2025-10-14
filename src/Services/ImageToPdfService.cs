using DocumentOcrProcessor.Models;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;

namespace DocumentOcrProcessor.Services;

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
            var document = new PdfDocument();

            foreach (var page in pages.OrderBy(p => p.PageNumber))
            {
                page.ImageStream.Position = 0;
                
                using var image = XImage.FromStream(() => page.ImageStream);
                
                var pdfPage = document.AddPage();
                pdfPage.Width = image.PixelWidth;
                pdfPage.Height = image.PixelHeight;
                
                using var gfx = XGraphics.FromPdfPage(pdfPage);
                gfx.DrawImage(image, 0, 0, image.PixelWidth, image.PixelHeight);
                
                _logger.LogInformation("Added page {PageNumber} to PDF", page.PageNumber);
            }

            var pdfStream = new MemoryStream();
            document.Save(pdfStream, false);
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
