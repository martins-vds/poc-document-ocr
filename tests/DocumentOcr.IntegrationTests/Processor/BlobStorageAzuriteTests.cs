using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DocumentOcr.Common.Services;
using DocumentOcr.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentOcr.IntegrationTests.Processor;

/// <summary>
/// Round-trips a blob and a queue message through the local Azurite
/// emulator using <see cref="BlobStorageService"/>'s public surface. Skipped
/// automatically when Azurite is not running on the developer machine.
///
/// This is the seed integration test that exercises the storage boundary;
/// extend with full end-to-end pipeline tests (queue → PdfProcessorFunction →
/// blob output → Cosmos) once a Cosmos emulator container is provisioned in
/// CI.
/// </summary>
public sealed class BlobStorageAzuriteTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _azurite;

    public BlobStorageAzuriteTests(AzuriteFixture azurite)
    {
        _azurite = azurite;
    }

    [SkippableFact]
    public async Task UploadAndDownload_RoundTripsThroughAzurite()
    {
        Skip.IfNot(_azurite.IsAvailable, "Azurite is not running on 127.0.0.1:10000.");

        var blobClient = new BlobServiceClient(AzuriteFixture.ConnectionString);
        var container = blobClient.GetBlobContainerClient($"itest-{Guid.NewGuid():N}");
        await container.CreateIfNotExistsAsync();

        try
        {
            var service = new BlobStorageService(blobClient, NullLogger<BlobStorageService>.Instance);
            var payload = new byte[] { 1, 2, 3, 4, 5 };

            await service.UploadBlobAsync(container.Name, "round.bin", new MemoryStream(payload));
            using var stream = await service.DownloadBlobAsync(container.Name, "round.bin");
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            Assert.Equal(payload, memory.ToArray());
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [SkippableFact]
    public async Task QueueClient_SendAndReceive_RoundTripsThroughAzurite()
    {
        Skip.IfNot(_azurite.IsAvailable, "Azurite is not running on 127.0.0.1:10001.");

        var queueClient = new QueueClient(
            AzuriteFixture.ConnectionString,
            $"itest-{Guid.NewGuid():N}");
        await queueClient.CreateIfNotExistsAsync();

        try
        {
            await queueClient.SendMessageAsync("hello");
            var received = await queueClient.ReceiveMessageAsync();
            Assert.NotNull(received.Value);
            Assert.Equal("hello", received.Value.MessageText);
        }
        finally
        {
            await queueClient.DeleteIfExistsAsync();
        }
    }
}
