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
            _logger.LogError(ex, "Cosmos DB container not found. Please ensure the database and container exist. Database: {Database}, Container: {Container}", 
                _container.Database.Id, _container.Id);
            throw new InvalidOperationException(
                $"Cosmos DB container not found. Please create the database and container as documented in DEPLOYMENT.md. Database: {_container.Database.Id}, Container: {_container.Id}", 
                ex);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Error persisting document {Id} to Cosmos DB. Status code: {StatusCode}", entity.Id, ex.StatusCode);
            throw;
        }
    }
}
