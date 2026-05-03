using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using DocumentOcr.Processor.Models;
using DocumentOcr.Processor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocumentOcr.Processor.Functions;

public class PdfProcessorFunction
{
    private const string ProcessedDocumentsContainer = "processed-documents";
    private const string DefaultIdentifierFieldName = "identifier";

    private readonly ILogger<PdfProcessorFunction> _logger;
    private readonly IPdfToImageService _pdfToImageService;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly IDocumentAggregatorService _documentAggregatorService;
    private readonly IImageToPdfService _imageToPdfService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IOperationService _operationService;
    private readonly IDocumentSchemaMapperService _schemaMapper;
    private readonly string _identifierFieldName;

    public PdfProcessorFunction(
        ILogger<PdfProcessorFunction> logger,
        IPdfToImageService pdfToImageService,
        IDocumentIntelligenceService documentIntelligenceService,
        IDocumentAggregatorService documentAggregatorService,
        IImageToPdfService imageToPdfService,
        IBlobStorageService blobStorageService,
        ICosmosDbService cosmosDbService,
        IOperationService operationService,
        IDocumentSchemaMapperService schemaMapper,
        IConfiguration configuration)
    {
        _logger = logger;
        _pdfToImageService = pdfToImageService;
        _documentIntelligenceService = documentIntelligenceService;
        _documentAggregatorService = documentAggregatorService;
        _imageToPdfService = imageToPdfService;
        _blobStorageService = blobStorageService;
        _cosmosDbService = cosmosDbService;
        _operationService = operationService;
        _schemaMapper = schemaMapper;

        var configured = configuration["DocumentProcessing:IdentifierFieldName"];
        _identifierFieldName = string.IsNullOrWhiteSpace(configured) ? DefaultIdentifierFieldName : configured;
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

            // Step 3: Submit each (selected) image for OCR analysis.
            // Per feature 002, the page-loop is restricted to message.PageRange (default = all pages).
            _logger.LogInformation("Step 3: Submitting images for OCR analysis (PageRange='{PageRange}')", message.PageRange ?? "<all>");
            var pageResults = await RunOcrLoopAsync(imageStreams, message.PageRange, operation);
            if (pageResults is null)
            {
                // Operation already marked Failed or Cancelled by the helper.
                return;
            }

            // Update operation with selected-page count.
            operation.TotalDocuments = pageResults.Count;
            await _operationService.UpdateOperationAsync(operation);

            _logger.LogInformation("OCR analysis completed for {PageCount} selected page(s)", pageResults.Count);

            // Step 4: Aggregate results by identifier property
            _logger.LogInformation("Step 4: Aggregating pages by identifier field: {IdentifierFieldName}", _identifierFieldName);
            var aggregatedDocuments = _documentAggregatorService.AggregatePagesByIdentifier(pageResults, _identifierFieldName);
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

                if (await TrySkipDuplicateAsync(aggregatedDoc, processingResult, operation.Id))
                {
                    continue;
                }

                _logger.LogInformation("Creating PDF for document {Number} (Identifier: {Identifier}) with {PageCount} pages",
                    documentNumber, aggregatedDoc.Identifier, aggregatedDoc.Pages.Count);

                // Create PDF from images
                var pdfStreamResult = await _imageToPdfService.CreatePdfFromImagesAsync(aggregatedDoc.Pages);

                // Upload to storage
                var outputBlobName = $"{Path.GetFileNameWithoutExtension(message.BlobName)}_doc_{documentNumber}.pdf";
                var outputBlobClient = outputContainerClient.GetBlobClient(outputBlobName);

                pdfStreamResult.Position = 0;
                await outputBlobClient.UploadAsync(pdfStreamResult, overwrite: true);

                // FR-020 — emit a structured warning when any page in the
                // document had its identifier inferred (forward-filled).
                var inferredCount = aggregatedDoc.PageProvenance.Count(p => p.IdentifierSource == IdentifierSource.Inferred);
                if (inferredCount > 0)
                {
                    _logger.LogWarning(
                        "FR-020 inferred-identifier pages — OperationId={OperationId} Identifier={Identifier} InferredPages={InferredCount}",
                        operation.Id, aggregatedDoc.Identifier, inferredCount);
                }

                var documentResult = new DocumentResult
                {
                    DocumentNumber = documentNumber,
                    PageCount = aggregatedDoc.Pages.Count,
                    PageNumbers = aggregatedDoc.Pages.Select(p => p.PageNumber).OrderBy(p => p).ToList(),
                    Identifier = aggregatedDoc.Identifier,
                    ExtractedData = new Dictionary<string, object>(),
                    OutputBlobName = outputBlobName,
                };

                processingResult.Documents.Add(documentResult);
                _logger.LogInformation("Saved document {Number} to blob: {BlobName}", documentNumber, outputBlobName);

                // Persist to Cosmos DB via the schema mapper (T024 / T030).
                var blobUrl = outputBlobClient.Uri.ToString();
                var cosmosEntity = _schemaMapper.Map(
                    aggregatedDoc,
                    documentNumber,
                    message.BlobName,
                    blobUrl,
                    outputBlobName);
                cosmosEntity.OperationId = operation.Id;

                await _cosmosDbService.CreateDocumentAsync(cosmosEntity);

                // FR-013 — structured consolidation outcome.
                var populated = cosmosEntity.Schema.Values.Count(f => f.OcrValue is not null);
                var nullCount = cosmosEntity.Schema.Count - populated;
                _logger.LogInformation(
                    "FR-013 consolidation outcome - OperationId={OperationId} Identifier={Identifier} SourcePages={SourcePages} PopulatedFields={Populated} NullFields={NullCount}",
                    operation.Id, aggregatedDoc.Identifier, aggregatedDoc.Pages.Count, populated, nullCount);

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

    /// <summary>
    /// FR-019 — duplicate-identifier pre-check. If a record with the given
    /// identifier already exists in Cosmos DB, logs a warning, records the
    /// identifier in <paramref name="processingResult"/>, and returns
    /// <c>true</c> to signal the caller to skip the document. Operation
    /// still succeeds.
    /// </summary>
    internal async Task<bool> TrySkipDuplicateAsync(
        AggregatedDocument aggregatedDoc,
        ProcessingResult processingResult,
        string operationId)
    {
        var existing = await _cosmosDbService.GetByIdentifierAsync(aggregatedDoc.Identifier);
        if (existing is null)
        {
            return false;
        }

        _logger.LogWarning(
            "FR-019 duplicate skip — OperationId={OperationId} Identifier={Identifier} ExistingDocumentId={ExistingId}",
            operationId, aggregatedDoc.Identifier, existing.Id);
        processingResult.SkippedDuplicateIdentifiers.Add(aggregatedDoc.Identifier);
        return true;
    }

    /// <summary>
    /// Per feature 002 (FR-009 / FR-011 / SC-006): restricts the OCR page loop to the
    /// pages selected by <paramref name="pageRange"/>. Disposes streams for excluded
    /// pages up front; addresses the existing <paramref name="imageStreams"/> list
    /// by <c>selectedPage - 1</c> (no re-decode). The per-page <c>PageNumber</c>
    /// assigned to each <see cref="PageOcrResult"/> is the document-local index
    /// (1..N) within the selected subset, NOT the original PDF page number, so
    /// downstream provenance and citations remain document-local.
    ///
    /// Returns <c>null</c> when the operation was marked <c>Failed</c> (parse/bounds
    /// error) or <c>Cancelled</c> (user requested cancellation mid-loop). The caller
    /// must short-circuit in that case; the helper has already updated the operation.
    /// </summary>
    internal async Task<List<PageOcrResult>?> RunOcrLoopAsync(
        IReadOnlyList<Stream> imageStreams,
        string? pageRange,
        Operation operation)
    {
        if (!PageSelection.TryParse(pageRange, maxPage: imageStreams.Count, out var selection, out var parseError))
        {
            foreach (var s in imageStreams) s.Dispose();
            operation.Status = OperationStatus.Failed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.Error = parseError;
            await _operationService.UpdateOperationAsync(operation);
            _logger.LogError("PageRange '{PageRange}' rejected for operation {OperationId}: {Error}", pageRange, operation.Id, parseError);
            return null;
        }

        var selectedPages = selection.Resolve(imageStreams.Count);
        var selectedSet = new HashSet<int>(selectedPages);

        // Dispose streams for excluded pages immediately.
        for (int i = 0; i < imageStreams.Count; i++)
        {
            if (!selectedSet.Contains(i + 1))
            {
                imageStreams[i].Dispose();
            }
        }

        var pageResults = new List<PageOcrResult>(selectedPages.Count);
        for (int i = 0; i < selectedPages.Count; i++)
        {
            var currentOp = await _operationService.GetOperationAsync(operation.Id);
            if (currentOp?.CancelRequested == true)
            {
                _logger.LogInformation("Operation {OperationId} cancelled during processing", operation.Id);
                operation.Status = OperationStatus.Cancelled;
                operation.CompletedAt = DateTime.UtcNow;
                await _operationService.UpdateOperationAsync(operation);

                foreach (var pr in pageResults) pr.ImageStream.Dispose();
                // Also dispose any not-yet-processed selected streams.
                for (int j = i; j < selectedPages.Count; j++)
                {
                    imageStreams[selectedPages[j] - 1].Dispose();
                }
                return null;
            }

            var pageNumber = i + 1; // FR-011: document-local 1..N, NOT the original PDF page number.
            var imageStream = imageStreams[selectedPages[i] - 1];

            _logger.LogInformation(
                "Analyzing page {PageNumber} of {Total} (source PDF page {SourcePage})",
                pageNumber, selectedPages.Count, selectedPages[i]);
            var extractedData = await _documentIntelligenceService.AnalyzeDocumentAsync(imageStream);

            pageResults.Add(new PageOcrResult
            {
                PageNumber = pageNumber,
                ImageStream = imageStream,
                ExtractedData = extractedData,
            });
        }
        return pageResults;
    }
}

public class QueueMessageWrapper
{
    public string? OperationId { get; set; }
    public QueueMessage? Message { get; set; }
}
