using Azure.Identity;
using Azure.Storage.Blobs;
using DocumentOcr.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentOcr.Common.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly ILogger<BlobStorageService> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger;
    }

    /// <summary>
    /// Convenience constructor used by the DI container in production. Builds a
    /// <see cref="BlobServiceClient"/> from configuration using Managed Identity.
    /// </summary>
    public BlobStorageService(ILogger<BlobStorageService> logger, IConfiguration configuration)
        : this(CreateClient(configuration), logger)
    {
    }

    internal static BlobServiceClient CreateClient(IConfiguration configuration)
    {
        var storageAccountName = configuration["Storage:AccountName"];

        if (string.IsNullOrEmpty(storageAccountName))
        {
            throw new InvalidOperationException("Storage account name is missing. Please configure Storage:AccountName.");
        }

        var blobServiceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
        return new BlobServiceClient(blobServiceUri, new DefaultAzureCredential());
    }

    public async Task<Stream> DownloadBlobAsync(string containerName, string blobName)
    {
        _logger.LogInformation("Downloading blob: {BlobName} from container: {ContainerName}", blobName, containerName);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var stream = new MemoryStream();
        await blobClient.DownloadToAsync(stream);
        stream.Position = 0;

        return stream;
    }

    public async Task UploadBlobAsync(string containerName, string blobName, Stream content, bool overwrite = true)
    {
        _logger.LogInformation("Uploading blob: {BlobName} to container: {ContainerName}", blobName, containerName);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, overwrite);
    }

    public async Task<BlobContainerClient> GetContainerClientAsync(string containerName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();
        return containerClient;
    }
}
