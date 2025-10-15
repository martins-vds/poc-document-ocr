using DocumentOcrProcessor.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentOcrProcessor.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly ILogger<CosmosDbService> _logger;
    private readonly Container _container;

    public CosmosDbService(ILogger<CosmosDbService> logger, IConfiguration configuration, CosmosClient cosmosClient)
    {
        _logger = logger;

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "DocumentOcrDb";
        var containerName = configuration["CosmosDb:ContainerName"] ?? "ProcessedDocuments";

        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    public async Task<DocumentOcrEntity> CreateDocumentAsync(DocumentOcrEntity entity)
    {
        try
        {
            _logger.LogInformation("Persisting document {Id} to Cosmos DB", entity.Id);
            var response = await _container.CreateItemAsync(entity, new PartitionKey(entity.Identifier));
            _logger.LogInformation("Successfully persisted document {Id} to Cosmos DB", entity.Id);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogError(ex, "Cosmos DB container not found. Please ensure the database and container exist. Database: {Database}, Container: {Container}", _container.Database.Id, _container.Id);
            var errorMessage = $"Cosmos DB container not found. Please create the database and container as documented in DEPLOYMENT.md. Database: {_container.Database.Id}, Container: {_container.Id}";
            throw new InvalidOperationException(errorMessage, ex);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error persisting document {Id} to Cosmos DB. Status code: {StatusCode}", entity.Id, ex.StatusCode);
            throw;
        }
    }

    public async Task<DocumentOcrEntity> UpdateDocumentAsync(DocumentOcrEntity entity)
    {
        try
        {
            _logger.LogInformation("Updating document {Id} in Cosmos DB", entity.Id);
            var response = await _container.ReplaceItemAsync(entity, entity.Id, new PartitionKey(entity.Identifier));
            _logger.LogInformation("Successfully updated document {Id} in Cosmos DB", entity.Id);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error updating document {Id} in Cosmos DB. Status code: {StatusCode}", entity.Id, ex.StatusCode);
            throw;
        }
    }

    public async Task<DocumentOcrEntity?> GetDocumentByIdAsync(string id, string partitionKey)
    {
        try
        {
            _logger.LogInformation("Retrieving document {Id} from Cosmos DB", id);
            var response = await _container.ReadItemAsync<DocumentOcrEntity>(id, new PartitionKey(partitionKey));
            _logger.LogInformation("Successfully retrieved document {Id} from Cosmos DB", id);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Document {Id} not found in Cosmos DB", id);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error retrieving document {Id} from Cosmos DB. Status code: {StatusCode}", id, ex.StatusCode);
            throw;
        }
    }

    public async Task<List<DocumentOcrEntity>> GetDocumentsAsync(string? reviewStatus = null, int? maxItems = null)
    {
        try
        {
            _logger.LogInformation("Querying documents from Cosmos DB with reviewStatus: {ReviewStatus}, maxItems: {MaxItems}", reviewStatus, maxItems);

            var queryText = reviewStatus != null
                ? "SELECT * FROM c WHERE c.reviewStatus = @reviewStatus ORDER BY c.processedAt DESC"
                : "SELECT * FROM c ORDER BY c.processedAt DESC";

            var queryDefinition = new QueryDefinition(queryText);
            if (reviewStatus != null)
            {
                queryDefinition = queryDefinition.WithParameter("@reviewStatus", reviewStatus);
            }

            var query = _container.GetItemQueryIterator<DocumentOcrEntity>(
                queryDefinition,
                requestOptions: new QueryRequestOptions { MaxItemCount = maxItems }
            );

            var results = new List<DocumentOcrEntity>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response);
            }

            _logger.LogInformation("Successfully retrieved {Count} documents from Cosmos DB", results.Count);
            return results;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error querying documents from Cosmos DB. Status code: {StatusCode}", ex.StatusCode);
            throw;
        }
    }
}
