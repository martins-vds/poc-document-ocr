using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using DocumentOcr.Processor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentOcr.UnitTests.Services;

public class QueueServiceTests
{
    [Fact]
    public void Constructor_NullClient_Throws()
    {
        var logger = new Mock<ILogger<QueueService>>();
        Assert.Throws<ArgumentNullException>(() => new QueueService((QueueClient)null!, logger.Object));
    }

    [Fact]
    public async Task SendMessageAsync_CreatesQueueAndSends()
    {
        var queue = new Mock<QueueClient>();
        queue.Setup(q => q.CreateIfNotExistsAsync(
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());
        queue.Setup(q => q.SendMessageAsync("hello"))
            .ReturnsAsync(Mock.Of<Response<SendReceipt>>());

        var service = new QueueService(queue.Object, new Mock<ILogger<QueueService>>().Object);

        await service.SendMessageAsync("hello");

        queue.Verify(q => q.CreateIfNotExistsAsync(
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        queue.Verify(q => q.SendMessageAsync("hello"), Times.Once);
    }

    [Fact]
    public void CreateQueueClient_NoConfiguration_Throws()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["AzureWebJobsStorage:queueServiceUri"]).Returns((string?)null);
        config.Setup(c => c["AzureWebJobsStorage"]).Returns((string?)null);

        Assert.Throws<InvalidOperationException>(() => QueueService.CreateQueueClient(config.Object));
    }

    [Fact]
    public void CreateQueueClient_WithConnectionString_Succeeds()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["AzureWebJobsStorage:queueServiceUri"]).Returns((string?)null);
        config.Setup(c => c["AzureWebJobsStorage"]).Returns("UseDevelopmentStorage=true");

        var client = QueueService.CreateQueueClient(config.Object);
        Assert.NotNull(client);
        Assert.Equal("pdf-processing-queue", client.Name);
    }

    [Theory]
    [InlineData("https://acct.queue.core.windows.net")]
    [InlineData("https://acct.queue.core.windows.net/")]
    public void CreateQueueClient_WithServiceUri_AppendsQueueName(string uri)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["AzureWebJobsStorage:queueServiceUri"]).Returns(uri);

        var client = QueueService.CreateQueueClient(config.Object);
        Assert.NotNull(client);
        Assert.Equal("pdf-processing-queue", client.Name);
    }

    [Fact]
    public void CreateQueueClient_WithInvalidServiceUri_Throws()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["AzureWebJobsStorage:queueServiceUri"]).Returns("not a uri");

        Assert.Throws<InvalidOperationException>(() => QueueService.CreateQueueClient(config.Object));
    }
}
