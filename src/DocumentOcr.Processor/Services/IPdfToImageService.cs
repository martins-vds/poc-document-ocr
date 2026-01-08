namespace DocumentOcr.Processor.Services;

public interface IPdfToImageService
{
    Task<List<Stream>> ConvertPdfPagesToImagesAsync(Stream pdfStream);
}
