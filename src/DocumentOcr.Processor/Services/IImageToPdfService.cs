using DocumentOcr.Processor.Models;

namespace DocumentOcr.Processor.Services;

public interface IImageToPdfService
{
    Task<Stream> CreatePdfFromImagesAsync(List<PageOcrResult> pages);
}
