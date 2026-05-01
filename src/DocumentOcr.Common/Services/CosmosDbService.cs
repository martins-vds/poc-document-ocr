using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentOcr.Common.Services;

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

    /// <summary>
    /// T023 — ETag-conditional replace per data-model.md and research.md D1.
    /// Sets <c>IfMatchEtag = entity.ETag</c>; surfaces a 412 as <c>CosmosException</c>.
    /// </summary>
    public async Task<DocumentOcrEntity> ReplaceWithETagAsync(DocumentOcrEntity entity, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("ETag-conditional replace for document {Id} (etag {ETag})", entity.Id, entity.ETag);
            var options = new ItemRequestOptions { IfMatchEtag = entity.ETag };
            var response = await _container.ReplaceItemAsync(
                entity,
                entity.Id,
                new PartitionKey(entity.Identifier),
                options,
                cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            _logger.LogWarning("ETag conflict on replace for document {Id}", entity.Id);
            throw;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error replacing document {Id} (status {StatusCode})", entity.Id, ex.StatusCode);
            throw;
        }
    }

    /// <summary>
    /// T023 / T018a — Single-partition point query for the existing record
    /// (if any) with the given <c>identifier</c> (= partition key).
    /// </summary>
    public async Task<DocumentOcrEntity?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }

        try
        {
            var query = new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.identifier = @id")
                .WithParameter("@id", identifier);

            var iterator = _container.GetItemQueryIterator<DocumentOcrEntity>(
                query,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(identifier), MaxItemCount = 1 });

            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                foreach (var item in page)
                {
                    return item;
                }
            }

            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error querying by identifier {Identifier} (status {StatusCode})", identifier, ex.StatusCode);
            throw;
        }
    }
}
