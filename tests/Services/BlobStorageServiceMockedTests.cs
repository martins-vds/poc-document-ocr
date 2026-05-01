using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocumentOcr.Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentOcr.Tests.Services;

public class BlobStorageServiceMockedTests
{
    private static (BlobStorageService Service, Mock<BlobServiceClient> ServiceClient, Mock<BlobContainerClient> Container, Mock<BlobClient> Blob) BuildService()
    {
        var blob = new Mock<BlobClient>();
        var container = new Mock<BlobContainerClient>();
        container.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(blob.Object);
        container.Setup(c => c.CreateIfNotExistsAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<BlobContainerEncryptionScopeOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<BlobContainerInfo>)null!);

        var serviceClient = new Mock<BlobServiceClient>();
        serviceClient.Setup(s => s.GetBlobContainerClient(It.IsAny<string>())).Returns(container.Object);

        var logger = new Mock<ILogger<BlobStorageService>>();
        return (new BlobStorageService(serviceClient.Object, logger.Object), serviceClient, container, blob);
    }

    [Fact]
    public void Constructor_NullClient_Throws()
    {
        var logger = new Mock<ILogger<BlobStorageService>>();
        Assert.Throws<ArgumentNullException>(() => new BlobStorageService(null!, logger.Object));
    }

    [Fact]
    public async Task DownloadBlobAsync_ReturnsRewoundStream()
    {
        var (service, _, container, blob) = BuildService();
        blob.Setup(b => b.DownloadToAsync(It.IsAny<Stream>()))
            .Callback<Stream>(s => s.WriteByte(0xAB))
            .ReturnsAsync((Response)null!);

        var stream = await service.DownloadBlobAsync("c", "b");

        Assert.Equal(0, stream.Position);
        Assert.Equal(1, stream.Length);
        container.Verify(c => c.GetBlobClient("b"), Times.Once);
    }

    [Fact]
    public async Task UploadBlobAsync_CreatesContainerAndUploads()
    {
        var (service, _, container, blob) = BuildService();
        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<BlobContentInfo>)null!);

        using var data = new MemoryStream(new byte[] { 1, 2, 3 });
        await service.UploadBlobAsync("c", "b", data);

        container.Verify(c => c.CreateIfNotExistsAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<BlobContainerEncryptionScopeOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
        blob.Verify(b => b.UploadAsync(data, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetContainerClientAsync_CreatesAndReturnsContainer()
    {
        var (service, _, container, _) = BuildService();

        var result = await service.GetContainerClientAsync("c");

        Assert.Same(container.Object, result);
        container.Verify(c => c.CreateIfNotExistsAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<BlobContainerEncryptionScopeOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateClient_MissingAccountName_Throws()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Storage:AccountName"]).Returns((string?)null);
        Assert.Throws<InvalidOperationException>(() => BlobStorageService.CreateClient(config.Object));
    }

    [Fact]
    public void CreateClient_WithAccountName_ReturnsClient()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Storage:AccountName"]).Returns("acct");
        var client = BlobStorageService.CreateClient(config.Object);
        Assert.NotNull(client);
        Assert.Equal("acct", client.AccountName);
    }
}
