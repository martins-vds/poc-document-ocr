using DocumentOcrProcessor.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentOcrProcessor.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly ILogger<CosmosDbService> _logger;
    private readonly Container _container;

    public CosmosDbService(ILogger<CosmosDbService> logger, IConfiguration configuration)
    {
        _logger = logger;

        var endpoint = configuration["CosmosDb:Endpoint"];
        var key = configuration["CosmosDb:Key"];
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "DocumentOcrDb";
        var containerName = configuration["CosmosDb:ContainerName"] ?? "ProcessedDocuments";

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("Cosmos DB configuration is missing. Please configure CosmosDb:Endpoint and CosmosDb:Key.");
        }

        var cosmosClient = new CosmosClient(endpoint, key);
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
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error persisting document {Id} to Cosmos DB. Status code: {StatusCode}", entity.Id, ex.StatusCode);
            throw;
        }
    }
}
