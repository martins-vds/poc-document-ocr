using System.Text.Json;
using Azure.Storage.Blobs;
using DocumentOcrProcessor.Models;
using DocumentOcrProcessor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentOcrProcessor.Functions;

public class PdfProcessorFunction
{
    private readonly ILogger<PdfProcessorFunction> _logger;
    private readonly IPdfSplitterService _pdfSplitterService;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly IBlobStorageService _blobStorageService;

    public PdfProcessorFunction(
        ILogger<PdfProcessorFunction> logger,
        IPdfSplitterService pdfSplitterService,
        IDocumentIntelligenceService documentIntelligenceService,
        IBlobStorageService blobStorageService)
    {
        _logger = logger;
        _pdfSplitterService = pdfSplitterService;
        _documentIntelligenceService = documentIntelligenceService;
        _blobStorageService = blobStorageService;
    }

    [Function("PdfProcessorFunction")]
    public async Task Run(
        [QueueTrigger("pdf-processing-queue", Connection = "AzureWebJobsStorage")] string queueMessage)
    {
        _logger.LogInformation("Processing PDF: {Message}", queueMessage);

        try
        {
            var message = JsonSerializer.Deserialize<QueueMessage>(queueMessage);
            if (message == null)
            {
                _logger.LogError("Failed to deserialize queue message");
                return;
            }

            using var pdfStream = await _blobStorageService.DownloadBlobAsync(message.ContainerName, message.BlobName);

            _logger.LogInformation("Performing OCR on entire PDF");
            var entirePdfOcrResult = await _documentIntelligenceService.AnalyzeDocumentAsync(pdfStream);
            var ocrText = entirePdfOcrResult.ContainsKey("Content") ? entirePdfOcrResult["Content"].ToString() : string.Empty;
            _logger.LogInformation("OCR completed for entire PDF");

            var splitDocuments = await _pdfSplitterService.SplitPdfIntoDocumentsAsync(pdfStream, ocrText ?? string.Empty, message.UseManualDetection);
            _logger.LogInformation("Split PDF into {Count} documents", splitDocuments.Count);

            var processingResult = new ProcessingResult
            {
                OriginalFileName = message.BlobName,
                TotalDocuments = splitDocuments.Count
            };

            var outputContainerClient = await _blobStorageService.GetContainerClientAsync("processed-documents");

            for (int i = 0; i < splitDocuments.Count; i++)
            {
                var document = splitDocuments[i];
                var documentNumber = i + 1;

                _logger.LogInformation("Processing document {Number} of {Total}", documentNumber, splitDocuments.Count);

                var extractedData = await _documentIntelligenceService.AnalyzeDocumentAsync(document);

                var outputBlobName = $"{Path.GetFileNameWithoutExtension(message.BlobName)}_doc_{documentNumber}.pdf";
                var outputBlobClient = outputContainerClient.GetBlobClient(outputBlobName);

                document.Position = 0;
                await outputBlobClient.UploadAsync(document, overwrite: true);

                var documentResult = new DocumentResult
                {
                    DocumentNumber = documentNumber,
                    PageCount = extractedData.ContainsKey("PageCount") ? Convert.ToInt32(extractedData["PageCount"]) : 0,
                    ExtractedData = extractedData,
                    OutputBlobName = outputBlobName
                };

                processingResult.Documents.Add(documentResult);
                _logger.LogInformation("Saved document {Number} to blob: {BlobName}", documentNumber, outputBlobName);

                document.Dispose();
            }

            var resultJson = JsonSerializer.Serialize(processingResult, new JsonSerializerOptions { WriteIndented = true });
            var resultBlobName = $"{Path.GetFileNameWithoutExtension(message.BlobName)}_result.json";
            var resultBlobClient = outputContainerClient.GetBlobClient(resultBlobName);
            
            using var resultStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(resultJson));
            await resultBlobClient.UploadAsync(resultStream, overwrite: true);

            _logger.LogInformation("Processing complete. Result saved to: {ResultBlobName}", resultBlobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PDF");
            throw;
        }
    }
}
