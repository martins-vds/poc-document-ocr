using DocumentOcrProcessor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentOcrWebApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PdfController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<PdfController> _logger;

    public PdfController(
        IBlobStorageService blobStorageService,
        ICosmosDbService cosmosDbService,
        ILogger<PdfController> logger)
    {
        _blobStorageService = blobStorageService;
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    [HttpGet("{id}/{identifier}")]
    public async Task<IActionResult> GetPdf(string id, string identifier)
    {
        try
        {
            var document = await _cosmosDbService.GetDocumentByIdAsync(id, identifier);
            
            if (document == null)
            {
                _logger.LogWarning("Document not found: {Id}/{Identifier}", id, identifier);
                return NotFound("Document not found");
            }

            if (string.IsNullOrEmpty(document.ContainerName) || string.IsNullOrEmpty(document.BlobName))
            {
                _logger.LogWarning("Document blob information missing: {Id}/{Identifier}", id, identifier);
                return NotFound("PDF not available");
            }

            var stream = await _blobStorageService.DownloadBlobAsync(document.ContainerName, document.BlobName);
            
            return File(stream, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PDF for document: {Id}/{Identifier}", id, identifier);
            return StatusCode(500, "Error retrieving PDF");
        }
    }
}
