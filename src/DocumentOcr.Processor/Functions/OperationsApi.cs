using Azure.Identity;
using Azure.Storage.Queues;
using DocumentOcr.Processor.Models;
using DocumentOcr.Processor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocumentOcr.Processor.Functions;

public class OperationsApi
{
    private readonly ILogger<OperationsApi> _logger;
    private readonly IOperationService _operationService;
    private readonly IConfiguration _configuration;

    public OperationsApi(
        ILogger<OperationsApi> logger,
        IOperationService operationService,
        IConfiguration configuration)
    {
        _logger = logger;
        _operationService = operationService;
        _configuration = configuration;
    }

    [Function("StartOperation")]
    public async Task<HttpResponseData> StartOperation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "operations")] HttpRequestData req)
    {
        _logger.LogInformation("Received request to start a new operation");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var startRequest = JsonSerializer.Deserialize<StartOperationRequest>(requestBody);

            if (startRequest == null || string.IsNullOrEmpty(startRequest.BlobName) || string.IsNullOrEmpty(startRequest.ContainerName))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("BlobName and ContainerName are required");
                return badResponse;
            }

            // Create operation record
            var operation = await _operationService.CreateOperationAsync(
                startRequest.BlobName,
                startRequest.ContainerName,
                startRequest.IdentifierFieldName ?? "identifier");

            // Queue the processing message
            var queueMessage = new QueueMessage
            {
                BlobName = operation.BlobName,
                ContainerName = operation.ContainerName,
                IdentifierFieldName = operation.IdentifierFieldName
            };

            var queueServiceUriString = _configuration["AzureWebJobsStorage:queueServiceUri"];
            QueueClient? queueClient;

            if (!string.IsNullOrEmpty(queueServiceUriString))
            {
                if (queueServiceUriString.EndsWith('/'))
                {
                    queueServiceUriString = $"{queueServiceUriString}pdf-processing-queue";
                }
                else
                {
                    queueServiceUriString = $"{queueServiceUriString}/pdf-processing-queue";
                }

                if (!Uri.TryCreate(queueServiceUriString, new UriCreationOptions(), out var queueServiceUri))
                {
                    throw new InvalidOperationException("Invalid queue service URI");
                }

                queueClient = new QueueClient(queueServiceUri, new DefaultAzureCredential());
            }
            else
            {
                var queueServiceConnectionString = _configuration["AzureWebJobsStorage"];

                if (string.IsNullOrEmpty(queueServiceConnectionString))
                {
                    throw new InvalidOperationException("Queue service URI or connection string must be provided in configuration");
                }

                queueClient = new QueueClient(queueServiceConnectionString, "pdf-processing-queue");
            }

            await queueClient.CreateIfNotExistsAsync();
            var messageContent = JsonSerializer.Serialize(queueMessage);

            // Store operation ID in the queue message metadata using a wrapper
            var queueMessageWrapper = new
            {
                OperationId = operation.Id,
                Message = queueMessage
            };
            var messageWithId = JsonSerializer.Serialize(queueMessageWrapper);

            await queueClient.SendMessageAsync(messageWithId);

            // Set resource URL for status polling
            var baseUrl = req.Url.GetLeftPart(UriPartial.Authority);
            operation.ResourceUrl = $"{baseUrl}/api/operations/{operation.Id}";
            operation = await _operationService.UpdateOperationAsync(operation);

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            response.Headers.Add("Location", operation.ResourceUrl);
            await response.WriteAsJsonAsync(new
            {
                operationId = operation.Id,
                status = operation.Status.ToString(),
                statusQueryGetUri = operation.ResourceUrl
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting operation");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error starting operation");
            return errorResponse;
        }
    }

    [Function("GetOperation")]
    public async Task<HttpResponseData> GetOperation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "operations/{operationId}")] HttpRequestData req,
        string operationId)
    {
        _logger.LogInformation("Retrieving operation {OperationId}", operationId);

        try
        {
            var operation = await _operationService.GetOperationAsync(operationId);

            if (operation == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Operation {operationId} not found");
                return notFoundResponse;
            }

            HttpStatusCode statusCode = operation.Status switch
            {
                OperationStatus.Running or OperationStatus.NotStarted => HttpStatusCode.Accepted,
                OperationStatus.Succeeded => HttpStatusCode.OK,
                OperationStatus.Failed => HttpStatusCode.InternalServerError,
                OperationStatus.Cancelled => HttpStatusCode.OK,
                _ => HttpStatusCode.OK
            };

            var response = req.CreateResponse(statusCode);

            if (operation.Status == OperationStatus.Running || operation.Status == OperationStatus.NotStarted)
            {
                response.Headers.Add("Retry-After", "10");
            }

            await response.WriteAsJsonAsync(new
            {
                operationId = operation.Id,
                status = operation.Status.ToString(),
                blobName = operation.BlobName,
                containerName = operation.ContainerName,
                createdAt = operation.CreatedAt,
                startedAt = operation.StartedAt,
                completedAt = operation.CompletedAt,
                processedDocuments = operation.ProcessedDocuments,
                totalDocuments = operation.TotalDocuments,
                resultBlobName = operation.ResultBlobName,
                error = operation.Error,
                cancelRequested = operation.CancelRequested
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving operation {OperationId}", operationId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error retrieving operation");
            return errorResponse;
        }
    }

    [Function("CancelOperation")]
    public async Task<HttpResponseData> CancelOperation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "operations/{operationId}/cancel")] HttpRequestData req,
        string operationId)
    {
        _logger.LogInformation("Cancelling operation {OperationId}", operationId);

        try
        {
            var operation = await _operationService.CancelOperationAsync(operationId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                operationId = operation.Id,
                status = operation.Status.ToString(),
                message = "Operation cancelled successfully"
            });

            return response;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot cancel operation {OperationId}", operationId);
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync(ex.Message);
            return badResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling operation {OperationId}", operationId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error cancelling operation");
            return errorResponse;
        }
    }

    [Function("RetryOperation")]
    public async Task<HttpResponseData> RetryOperation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "operations/{operationId}/retry")] HttpRequestData req,
        string operationId)
    {
        _logger.LogInformation("Retrying operation {OperationId}", operationId);

        try
        {
            var operation = await _operationService.GetOperationAsync(operationId);

            if (operation == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Operation {operationId} not found");
                return notFoundResponse;
            }

            if (operation.Status != OperationStatus.Failed && operation.Status != OperationStatus.Cancelled && operation.Status != OperationStatus.Succeeded)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync($"Cannot retry operation in {operation.Status} status. Only Failed, Cancelled, or Succeeded operations can be retried.");
                return badResponse;
            }

            // Create a new operation with the same parameters
            var newOperation = await _operationService.CreateOperationAsync(
                operation.BlobName,
                operation.ContainerName,
                operation.IdentifierFieldName);

            // Queue the processing message
            var queueMessage = new QueueMessage
            {
                BlobName = newOperation.BlobName,
                ContainerName = newOperation.ContainerName,
                IdentifierFieldName = newOperation.IdentifierFieldName
            };

            var connectionString = _configuration["AzureWebJobsStorage"];
            var queueClient = new QueueClient(connectionString, "pdf-processing-queue");
            await queueClient.CreateIfNotExistsAsync();

            var messageContent = JsonSerializer.Serialize(queueMessage);

            // Store operation ID in the queue message metadata using a wrapper
            var queueMessageWrapper = new
            {
                OperationId = newOperation.Id,
                Message = queueMessage
            };
            var messageWithId = JsonSerializer.Serialize(queueMessageWrapper);

            await queueClient.SendMessageAsync(messageWithId);

            // Set resource URL for status polling
            var baseUrl = req.Url.GetLeftPart(UriPartial.Authority);
            newOperation.ResourceUrl = $"{baseUrl}/api/operations/{newOperation.Id}";
            newOperation = await _operationService.UpdateOperationAsync(newOperation);

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            response.Headers.Add("Location", newOperation.ResourceUrl);
            await response.WriteAsJsonAsync(new
            {
                operationId = newOperation.Id,
                status = newOperation.Status.ToString(),
                statusQueryGetUri = newOperation.ResourceUrl,
                message = "Operation retry started"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying operation {OperationId}", operationId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error retrying operation");
            return errorResponse;
        }
    }

    [Function("ListOperations")]
    public async Task<HttpResponseData> ListOperations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "operations")] HttpRequestData req)
    {
        _logger.LogInformation("Listing operations");

        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var statusParam = query["status"];
            var maxItemsParam = query["maxItems"];

            OperationStatus? status = null;
            if (!string.IsNullOrEmpty(statusParam) && Enum.TryParse<OperationStatus>(statusParam, true, out var parsedStatus))
            {
                status = parsedStatus;
            }

            int? maxItems = null;
            if (!string.IsNullOrEmpty(maxItemsParam) && int.TryParse(maxItemsParam, out var parsedMaxItems))
            {
                maxItems = parsedMaxItems;
            }

            var operations = await _operationService.GetOperationsAsync(status, maxItems);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                operations = operations.Select(op => new
                {
                    operationId = op.Id,
                    status = op.Status.ToString(),
                    blobName = op.BlobName,
                    containerName = op.ContainerName,
                    createdAt = op.CreatedAt,
                    startedAt = op.StartedAt,
                    completedAt = op.CompletedAt,
                    processedDocuments = op.ProcessedDocuments,
                    totalDocuments = op.TotalDocuments,
                    resultBlobName = op.ResultBlobName,
                    error = op.Error
                }).ToList(),
                count = operations.Count
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing operations");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error listing operations");
            return errorResponse;
        }
    }
}

public class StartOperationRequest
{
    [JsonPropertyName("blobName")]
    public string BlobName { get; set; } = string.Empty;
    [JsonPropertyName("containerName")]
    public string ContainerName { get; set; } = string.Empty;
    [JsonPropertyName("identifierFieldName")]
    public string? IdentifierFieldName { get; set; }
}
