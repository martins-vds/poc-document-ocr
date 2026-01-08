using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using DocumentOcr.Processor.Models;
using DocumentOcr.Processor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocumentOcr.Processor.Functions;

public class PdfProcessorFunction
{
    private const string ProcessedDocumentsContainer = "processed-documents";

    private readonly ILogger<PdfProcessorFunction> _logger;
    private readonly IPdfToImageService _pdfToImageService;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly IDocumentAggregatorService _documentAggregatorService;
    private readonly IImageToPdfService _imageToPdfService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IOperationService _operationService;

    public PdfProcessorFunction(
        ILogger<PdfProcessorFunction> logger,
        IPdfToImageService pdfToImageService,
        IDocumentIntelligenceService documentIntelligenceService,
        IDocumentAggregatorService documentAggregatorService,
        IImageToPdfService imageToPdfService,
        IBlobStorageService blobStorageService,
        ICosmosDbService cosmosDbService,
        IOperationService operationService)
    {
        _logger = logger;
        _pdfToImageService = pdfToImageService;
        _documentIntelligenceService = documentIntelligenceService;
        _documentAggregatorService = documentAggregatorService;
        _imageToPdfService = imageToPdfService;
        _blobStorageService = blobStorageService;
        _cosmosDbService = cosmosDbService;
        _operationService = operationService;
    }

    [Function("PdfProcessorFunction")]
    public async Task Run(
        [QueueTrigger("pdf-processing-queue", Connection = "AzureWebJobsStorage")] string queueMessage)
    {
        _logger.LogInformation("Processing PDF: {Message}", queueMessage);

        Operation? operation = null;

        try
        {
            // Deserialize the queue message with operation wrapper
            var messageWrapper = JsonSerializer.Deserialize<QueueMessageWrapper>(queueMessage);

            if (messageWrapper?.Message == null || string.IsNullOrEmpty(messageWrapper.OperationId))
            {
                _logger.LogError("Invalid queue message format. Expected QueueMessageWrapper with OperationId and Message.");
                return;
            }

            var operationId = messageWrapper.OperationId;
            var message = messageWrapper.Message;

            // Get operation
            operation = await _operationService.GetOperationAsync(operationId);
            if (operation == null)
            {
                _logger.LogError("Operation {OperationId} not found", operationId);
                return;
            }

            // Check for cancellation
            if (operation.CancelRequested)
            {
                _logger.LogInformation("Operation {OperationId} was cancelled", operationId);
                operation.Status = OperationStatus.Cancelled;
                operation.CompletedAt = DateTime.UtcNow;
                await _operationService.UpdateOperationAsync(operation);
                return;
            }

            // Update operation status to Running
            operation.Status = OperationStatus.Running;
            operation.StartedAt = DateTime.UtcNow;
            await _operationService.UpdateOperationAsync(operation);

            // Step 1: Download PDF from storage account
            _logger.LogInformation("Step 1: Downloading PDF from storage");
            using var pdfStream = await _blobStorageService.DownloadBlobAsync(message.ContainerName, message.BlobName);

            // Step 2: Split pages into individual image files
            _logger.LogInformation("Step 2: Converting PDF pages to images");
            var imageStreams = await _pdfToImageService.ConvertPdfPagesToImagesAsync(pdfStream);
            _logger.LogInformation("Converted {PageCount} pages to images", imageStreams.Count);

            // Update operation with total count
            operation.TotalDocuments = imageStreams.Count;
            await _operationService.UpdateOperationAsync(operation);

            // Step 3: Submit each image for OCR analysis in batch
            _logger.LogInformation("Step 3: Submitting images for OCR analysis");
            var pageResults = new List<PageOcrResult>();

            for (int i = 0; i < imageStreams.Count; i++)
            {
                // Check for cancellation before processing each page
                var currentOp = await _operationService.GetOperationAsync(operation.Id);
                if (currentOp?.CancelRequested == true)
                {
                    _logger.LogInformation("Operation {OperationId} cancelled during processing", operation.Id);
                    operation.Status = OperationStatus.Cancelled;
                    operation.CompletedAt = DateTime.UtcNow;
                    await _operationService.UpdateOperationAsync(operation);

                    // Clean up image streams
                    foreach (var pageResult in pageResults)
                    {
                        pageResult.ImageStream.Dispose();
                    }
                    return;
                }

                var pageNumber = i + 1;
                var imageStream = imageStreams[i];

                _logger.LogInformation("Analyzing page {PageNumber} of {Total}", pageNumber, imageStreams.Count);
                var extractedData = await _documentIntelligenceService.AnalyzeDocumentAsync(imageStream);

                pageResults.Add(new PageOcrResult
                {
                    PageNumber = pageNumber,
                    ImageStream = imageStream,
                    ExtractedData = extractedData
                });
            }

            _logger.LogInformation("OCR analysis completed for all {PageCount} pages", pageResults.Count);

            // Step 4: Aggregate results by identifier property
            _logger.LogInformation("Step 4: Aggregating pages by identifier field: {IdentifierFieldName}", message.IdentifierFieldName);
            var aggregatedDocuments = _documentAggregatorService.AggregatePagesByIdentifier(pageResults, message.IdentifierFieldName);
            _logger.LogInformation("Aggregated into {DocumentCount} documents", aggregatedDocuments.Count);

            // Update operation with aggregated count
            operation.TotalDocuments = aggregatedDocuments.Count;
            await _operationService.UpdateOperationAsync(operation);

            // Step 5: Create individual PDFs from aggregated pages and upload
            _logger.LogInformation("Step 5: Creating PDFs and uploading to storage");
            var processingResult = new ProcessingResult
            {
                OriginalFileName = message.BlobName,
                TotalDocuments = aggregatedDocuments.Count
            };

            var outputContainerClient = await _blobStorageService.GetContainerClientAsync(ProcessedDocumentsContainer);

            for (int i = 0; i < aggregatedDocuments.Count; i++)
            {
                var aggregatedDoc = aggregatedDocuments[i];
                var documentNumber = i + 1;

                _logger.LogInformation("Creating PDF for document {Number} (Identifier: {Identifier}) with {PageCount} pages",
                    documentNumber, aggregatedDoc.Identifier, aggregatedDoc.Pages.Count);

                // Create PDF from images
                var pdfStreamResult = await _imageToPdfService.CreatePdfFromImagesAsync(aggregatedDoc.Pages);

                // Upload to storage
                var outputBlobName = $"{Path.GetFileNameWithoutExtension(message.BlobName)}_doc_{documentNumber}.pdf";
                var outputBlobClient = outputContainerClient.GetBlobClient(outputBlobName);

                pdfStreamResult.Position = 0;
                await outputBlobClient.UploadAsync(pdfStreamResult, overwrite: true);

                // Combine extracted data from all pages
                var combinedExtractedData = new Dictionary<string, object>
                {
                    ["PageCount"] = aggregatedDoc.Pages.Count,
                    ["Pages"] = aggregatedDoc.Pages.Select(p => p.ExtractedData).ToList()
                };

                var documentResult = new DocumentResult
                {
                    DocumentNumber = documentNumber,
                    PageCount = aggregatedDoc.Pages.Count,
                    PageNumbers = aggregatedDoc.Pages.Select(p => p.PageNumber).OrderBy(p => p).ToList(),
                    Identifier = aggregatedDoc.Identifier,
                    ExtractedData = combinedExtractedData,
                    OutputBlobName = outputBlobName
                };

                processingResult.Documents.Add(documentResult);
                _logger.LogInformation("Saved document {Number} to blob: {BlobName}", documentNumber, outputBlobName);

                // Persist to Cosmos DB
                var blobUrl = outputBlobClient.Uri.ToString();
                var cosmosEntity = new DocumentOcrEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    DocumentNumber = documentNumber,
                    OriginalFileName = message.BlobName,
                    Identifier = aggregatedDoc.Identifier,
                    PageCount = aggregatedDoc.Pages.Count,
                    PageNumbers = aggregatedDoc.Pages.Select(p => p.PageNumber).OrderBy(p => p).ToList(),
                    PdfBlobUrl = blobUrl,
                    ExtractedData = combinedExtractedData,
                    ProcessedAt = DateTime.UtcNow,
                    ContainerName = ProcessedDocumentsContainer,
                    BlobName = outputBlobName
                };

                await _cosmosDbService.CreateDocumentAsync(cosmosEntity);

                pdfStreamResult.Dispose();

                // Update operation progress
                operation.ProcessedDocuments = documentNumber;
                await _operationService.UpdateOperationAsync(operation);
            }

            // Save processing result
            var resultJson = JsonSerializer.Serialize(processingResult, new JsonSerializerOptions { WriteIndented = true });
            var resultBlobName = $"{Path.GetFileNameWithoutExtension(message.BlobName)}_result.json";
            var resultBlobClient = outputContainerClient.GetBlobClient(resultBlobName);

            using var resultStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(resultJson));
            await resultBlobClient.UploadAsync(resultStream, overwrite: true);

            _logger.LogInformation("Processing complete. Result saved to: {ResultBlobName}", resultBlobName);

            // Update operation to succeeded
            operation.Status = OperationStatus.Succeeded;
            operation.CompletedAt = DateTime.UtcNow;
            operation.ResultBlobName = resultBlobName;
            await _operationService.UpdateOperationAsync(operation);

            // Clean up image streams
            foreach (var pageResult in pageResults)
            {
                pageResult.ImageStream.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PDF");

            // Update operation to failed
            if (operation != null)
            {
                operation.Status = OperationStatus.Failed;
                operation.CompletedAt = DateTime.UtcNow;
                operation.Error = ex.Message;
                await _operationService.UpdateOperationAsync(operation);
            }

            throw;
        }
    }
}

public class QueueMessageWrapper
{
    public string? OperationId { get; set; }
    public QueueMessage? Message { get; set; }
}
