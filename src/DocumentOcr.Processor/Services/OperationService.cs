using DocumentOcr.Processor.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentOcr.Processor.Services;

public class OperationService : IOperationService
{
    private readonly ILogger<OperationService> _logger;
    private readonly Container _container;

    public OperationService(ILogger<OperationService> logger, IConfiguration configuration, CosmosClient cosmosClient)
    {
        _logger = logger;

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "DocumentOcrDb";
        var containerName = configuration["CosmosDb:OperationsContainerName"] ?? "Operations";

        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    public async Task<Operation> CreateOperationAsync(string blobName, string containerName, string identifierFieldName = "identifier")
    {
        var operation = new Operation
        {
            BlobName = blobName,
            ContainerName = containerName,
            IdentifierFieldName = identifierFieldName,
            Status = OperationStatus.NotStarted,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Creating operation {OperationId} for blob {BlobName}", operation.Id, blobName);
            var response = await _container.CreateItemAsync(operation, new PartitionKey(operation.Id));
            _logger.LogInformation("Successfully created operation {OperationId}", operation.Id);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogError(ex, "Cosmos DB container not found. Please ensure the database and container exist. Database: {Database}, Container: {Container}", _container.Database.Id, _container.Id);
            var errorMessage = $"Cosmos DB container not found. Please create the database and container. Database: {_container.Database.Id}, Container: {_container.Id}";
            throw new InvalidOperationException(errorMessage, ex);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error creating operation {OperationId}. Status code: {StatusCode}", operation.Id, ex.StatusCode);
            throw;
        }
    }

    public async Task<Operation?> GetOperationAsync(string operationId)
    {
        try
        {
            _logger.LogInformation("Retrieving operation {OperationId}", operationId);
            var response = await _container.ReadItemAsync<Operation>(operationId, new PartitionKey(operationId));
            _logger.LogInformation("Successfully retrieved operation {OperationId}", operationId);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Operation {OperationId} not found", operationId);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error retrieving operation {OperationId}. Status code: {StatusCode}", operationId, ex.StatusCode);
            throw;
        }
    }

    public async Task<Operation> UpdateOperationAsync(Operation operation)
    {
        try
        {
            _logger.LogInformation("Updating operation {OperationId}", operation.Id);
            var response = await _container.ReplaceItemAsync(operation, operation.Id, new PartitionKey(operation.Id));
            _logger.LogInformation("Successfully updated operation {OperationId}", operation.Id);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error updating operation {OperationId}. Status code: {StatusCode}", operation.Id, ex.StatusCode);
            throw;
        }
    }

    public async Task<List<Operation>> GetOperationsAsync(OperationStatus? status = null, int? maxItems = null)
    {
        try
        {
            _logger.LogInformation("Querying operations with status: {Status}, maxItems: {MaxItems}", status, maxItems);

            var queryText = status.HasValue
                ? "SELECT * FROM c WHERE c.status = @status ORDER BY c.createdAt DESC"
                : "SELECT * FROM c ORDER BY c.createdAt DESC";

            var queryDefinition = new QueryDefinition(queryText);
            if (status.HasValue)
            {
                queryDefinition = queryDefinition.WithParameter("@status", status.Value.ToString());
            }

            var query = _container.GetItemQueryIterator<Operation>(
                queryDefinition,
                requestOptions: new QueryRequestOptions { MaxItemCount = maxItems }
            );

            var results = new List<Operation>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response);
            }

            _logger.LogInformation("Successfully retrieved {Count} operations", results.Count);
            return results;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error querying operations. Status code: {StatusCode}", ex.StatusCode);
            throw;
        }
    }

    public async Task<Operation> CancelOperationAsync(string operationId)
    {
        var operation = await GetOperationAsync(operationId);
        if (operation == null)
        {
            throw new InvalidOperationException($"Operation {operationId} not found");
        }

        if (operation.Status == OperationStatus.Succeeded || 
            operation.Status == OperationStatus.Failed || 
            operation.Status == OperationStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot cancel operation in {operation.Status} status");
        }

        operation.CancelRequested = true;
        
        if (operation.Status == OperationStatus.NotStarted)
        {
            operation.Status = OperationStatus.Cancelled;
            operation.CompletedAt = DateTime.UtcNow;
        }

        return await UpdateOperationAsync(operation);
    }
}
