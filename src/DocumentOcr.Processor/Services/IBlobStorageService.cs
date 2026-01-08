using Azure.Storage.Blobs;

namespace DocumentOcr.Processor.Services;

public interface IBlobStorageService
{
    Task<Stream> DownloadBlobAsync(string containerName, string blobName);
    Task UploadBlobAsync(string containerName, string blobName, Stream content, bool overwrite = true);
    Task<BlobContainerClient> GetContainerClientAsync(string containerName);
}
