using DocumentOcrProcessor.Models;

namespace DocumentOcrProcessor.Services;

public interface IImageToPdfService
{
    Task<Stream> CreatePdfFromImagesAsync(List<PageOcrResult> pages);
}
