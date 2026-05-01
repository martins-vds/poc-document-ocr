using System.Net;
using DocumentOcr.Processor.Models;
using DocumentOcr.Processor.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentOcr.Tests.Services;

/// <summary>
/// Coverage for <see cref="OperationService"/> — the queue-trigger
/// orchestrator's persistent state. Mirrors the
/// <see cref="CosmosDbServiceTests"/> mocking pattern.
/// </summary>
public class OperationServiceTests
{
    private static (OperationService service, Mock<Container> container) BuildService()
    {
        var containerMock = new Mock<Container>();
        containerMock.SetupGet(c => c.Id).Returns("Operations");
        var dbMock = new Mock<Database>();
        dbMock.SetupGet(d => d.Id).Returns("DocumentOcrDb");
        containerMock.SetupGet(c => c.Database).Returns(dbMock.Object);
        var clientMock = new Mock<CosmosClient>();
        clientMock.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(containerMock.Object);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CosmosDb:DatabaseName"]).Returns("DocumentOcrDb");
        configMock.Setup(c => c["CosmosDb:OperationsContainerName"]).Returns("Operations");

        return (new OperationService(NullLogger<OperationService>.Instance, configMock.Object, clientMock.Object), containerMock);
    }

    private static Mock<ItemResponse<Operation>> ItemResponse(Operation op)
    {
        var m = new Mock<ItemResponse<Operation>>();
        m.SetupGet(r => r.Resource).Returns(op);
        return m;
    }

    [Fact]
    public async Task CreateOperationAsync_PersistsOperationWithNotStartedStatus()
    {
        var (service, container) = BuildService();
        Operation? captured = null;
        container
            .Setup(c => c.CreateItemAsync(
                It.IsAny<Operation>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Operation, PartitionKey?, ItemRequestOptions, CancellationToken>((o, _, _, _) => captured = o)
            .ReturnsAsync((Operation o, PartitionKey? _, ItemRequestOptions _, CancellationToken _) => ItemResponse(o).Object);

        var op = await service.CreateOperationAsync("input.pdf", "incoming");

        Assert.NotNull(captured);
        Assert.Equal(OperationStatus.NotStarted, op.Status);
        Assert.Equal("input.pdf", op.BlobName);
        Assert.Equal("incoming", op.ContainerName);
        Assert.False(string.IsNullOrEmpty(op.Id));
    }

    [Fact]
    public async Task CreateOperationAsync_ContainerMissing_RethrowsAsInvalidOperation()
    {
        var (service, container) = BuildService();
        container
            .Setup(c => c.CreateItemAsync(
                It.IsAny<Operation>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("nope", HttpStatusCode.NotFound, 0, "", 0));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOperationAsync("a", "b"));
    }

    [Fact]
    public async Task GetOperationAsync_WhenAbsent_ReturnsNull()
    {
        var (service, container) = BuildService();
        container
            .Setup(c => c.ReadItemAsync<Operation>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("missing", HttpStatusCode.NotFound, 0, "", 0));

        var result = await service.GetOperationAsync("op-x");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOperationAsync_WhenPresent_ReturnsOperation()
    {
        var (service, container) = BuildService();
        var op = new Operation { Id = "op-1", BlobName = "foo.pdf" };
        container
            .Setup(c => c.ReadItemAsync<Operation>(
                "op-1", It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemResponse(op).Object);

        var result = await service.GetOperationAsync("op-1");

        Assert.NotNull(result);
        Assert.Equal("foo.pdf", result!.BlobName);
    }

    [Fact]
    public async Task UpdateOperationAsync_ReplacesItem()
    {
        var (service, container) = BuildService();
        var op = new Operation { Id = "op-1", Status = OperationStatus.Running };
        container
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<Operation>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemResponse(op).Object);

        var result = await service.UpdateOperationAsync(op);

        Assert.Equal(OperationStatus.Running, result.Status);
        container.Verify(c => c.ReplaceItemAsync(
            op, "op-1", It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOperationsAsync_WithStatusFilter_ParameterisesQuery()
    {
        var (service, container) = BuildService();
        var op = new Operation { Id = "op-1" };

        var feed = new Mock<FeedResponse<Operation>>();
        feed.Setup(r => r.GetEnumerator()).Returns(new List<Operation> { op }.GetEnumerator());
        var iter = new Mock<FeedIterator<Operation>>();
        iter.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);
        iter.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(feed.Object);

        QueryDefinition? capturedQuery = null;
        container
            .Setup(c => c.GetItemQueryIterator<Operation>(
                It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((q, _, _) => capturedQuery = q)
            .Returns(iter.Object);

        var result = await service.GetOperationsAsync(OperationStatus.Running, maxItems: 5);

        Assert.Single(result);
        Assert.NotNull(capturedQuery);
        Assert.Contains("@status", capturedQuery!.QueryText);
        var statusParam = capturedQuery.GetQueryParameters()
            .Single(p => p.Name == "@status");
        Assert.Equal(OperationStatus.Running.ToString(), statusParam.Value?.ToString());
    }

    [Fact]
    public async Task GetOperationsAsync_WithoutFilter_OmitsWhereClause()
    {
        var (service, container) = BuildService();
        var iter = new Mock<FeedIterator<Operation>>();
        iter.SetupSequence(i => i.HasMoreResults).Returns(false);

        QueryDefinition? capturedQuery = null;
        container
            .Setup(c => c.GetItemQueryIterator<Operation>(
                It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((q, _, _) => capturedQuery = q)
            .Returns(iter.Object);

        await service.GetOperationsAsync();

        Assert.NotNull(capturedQuery);
        Assert.DoesNotContain("WHERE", capturedQuery!.QueryText);
    }

    [Fact]
    public async Task CancelOperationAsync_WhenMissing_Throws()
    {
        var (service, container) = BuildService();
        container
            .Setup(c => c.ReadItemAsync<Operation>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("missing", HttpStatusCode.NotFound, 0, "", 0));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelOperationAsync("nope"));
    }

    [Theory]
    [InlineData(OperationStatus.Succeeded)]
    [InlineData(OperationStatus.Failed)]
    [InlineData(OperationStatus.Cancelled)]
    public async Task CancelOperationAsync_WhenTerminal_Throws(OperationStatus terminal)
    {
        var (service, container) = BuildService();
        var op = new Operation { Id = "op-1", Status = terminal };
        container
            .Setup(c => c.ReadItemAsync<Operation>(
                "op-1", It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemResponse(op).Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelOperationAsync("op-1"));
    }

    [Fact]
    public async Task CancelOperationAsync_WhenNotStarted_TransitionsImmediatelyToCancelled()
    {
        var (service, container) = BuildService();
        var op = new Operation { Id = "op-1", Status = OperationStatus.NotStarted };
        container
            .Setup(c => c.ReadItemAsync<Operation>(
                "op-1", It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemResponse(op).Object);
        container
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<Operation>(), "op-1", It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Operation o, string _, PartitionKey? _, ItemRequestOptions _, CancellationToken _) => ItemResponse(o).Object);

        var result = await service.CancelOperationAsync("op-1");

        Assert.Equal(OperationStatus.Cancelled, result.Status);
        Assert.True(result.CancelRequested);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task CancelOperationAsync_WhenRunning_FlagsCancelRequestedButKeepsStatus()
    {
        var (service, container) = BuildService();
        var op = new Operation { Id = "op-1", Status = OperationStatus.Running };
        container
            .Setup(c => c.ReadItemAsync<Operation>(
                "op-1", It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemResponse(op).Object);
        container
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<Operation>(), "op-1", It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Operation o, string _, PartitionKey? _, ItemRequestOptions _, CancellationToken _) => ItemResponse(o).Object);

        var result = await service.CancelOperationAsync("op-1");

        Assert.True(result.CancelRequested);
        Assert.Equal(OperationStatus.Running, result.Status);
        Assert.Null(result.CompletedAt);
    }
}
