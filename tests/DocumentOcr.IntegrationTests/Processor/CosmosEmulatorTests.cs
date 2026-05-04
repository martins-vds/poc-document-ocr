using DocumentOcr.Common.Models;
using DocumentOcr.Common.Services;
using DocumentOcr.IntegrationTests.Fixtures;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentOcr.IntegrationTests.Processor;

/// <summary>
/// Round-trips a document through <see cref="CosmosDbService"/> against the
/// local Cosmos DB Emulator. Skipped when the emulator is not reachable on
/// https://localhost:8081.
/// </summary>
public sealed class CosmosEmulatorTests : IClassFixture<CosmosEmulatorFixture>
{
    private readonly CosmosEmulatorFixture _cosmos;

    public CosmosEmulatorTests(CosmosEmulatorFixture cosmos)
    {
        _cosmos = cosmos;
    }

    [SkippableFact]
    public async Task CreateAndRead_RoundTripsThroughEmulator()
    {
        Skip.IfNot(_cosmos.IsAvailable, "Cosmos DB Emulator is not running on https://localhost:8081.");
        Assert.NotNull(_cosmos.Client);

        var dbName = $"itest-db-{Guid.NewGuid():N}";
        var containerName = "ProcessedDocuments";

        var db = (await _cosmos.Client!.CreateDatabaseIfNotExistsAsync(dbName)).Database;
        try
        {
            await db.CreateContainerIfNotExistsAsync(containerName, "/identifier");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["CosmosDb:DatabaseName"] = dbName,
                    ["CosmosDb:ContainerName"] = containerName,
                }).Build();

            var service = new CosmosDbService(
                NullLogger<CosmosDbService>.Instance,
                config,
                _cosmos.Client);

            var entity = new DocumentOcrEntity
            {
                Id = Guid.NewGuid().ToString(),
                Identifier = "TK-EMU-1",
            };

            var created = await service.CreateDocumentAsync(entity);
            Assert.Equal(entity.Id, created.Id);

            var read = await service.GetDocumentByIdAsync(entity.Id, entity.Identifier);
            Assert.NotNull(read);
            Assert.Equal("TK-EMU-1", read!.Identifier);
        }
        finally
        {
            await db.DeleteAsync();
        }
    }
}
