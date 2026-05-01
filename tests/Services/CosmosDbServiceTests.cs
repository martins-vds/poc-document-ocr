using System.Net;
using DocumentOcr.Common.Models;
using DocumentOcr.Common.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentOcr.Tests.Services;

/// <summary>
/// T021 — covers CosmosDbService extensions: ETag-conditional replace
/// passes IfMatchEtag, surfaces 412 as CosmosException; identifier point
/// query returns the existing record or null.
/// </summary>
public class CosmosDbServiceTests
{
    private static (CosmosDbService service, Mock<Container> container) BuildService()
    {
        var containerMock = new Mock<Container>();

        var clientMock = new Mock<CosmosClient>();
        clientMock
            .Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(containerMock.Object);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["CosmosDb:DatabaseName"]).Returns("DocumentOcrDb");
        configMock.Setup(c => c["CosmosDb:ContainerName"]).Returns("ProcessedDocuments");

        var service = new CosmosDbService(
            NullLogger<CosmosDbService>.Instance,
            configMock.Object,
            clientMock.Object);

        return (service, containerMock);
    }

    private static DocumentOcrEntity SampleEntity(string id = "doc-1", string identifier = "TK-1", string etag = "\"abc\"") =>
        new()
        {
            Id = id,
            Identifier = identifier,
            ETag = etag,
        };

    [Fact]
    public async Task ReplaceWithETagAsync_PassesIfMatchEtag()
    {
        var (service, container) = BuildService();
        var entity = SampleEntity();

        ItemRequestOptions? captured = null;
        var responseMock = new Mock<ItemResponse<DocumentOcrEntity>>();
        responseMock.SetupGet(r => r.Resource).Returns(entity);

        container
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<DocumentOcrEntity>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<DocumentOcrEntity, string, PartitionKey?, ItemRequestOptions, CancellationToken>(
                (_, _, _, opts, _) => captured = opts)
            .ReturnsAsync(responseMock.Object);

        await service.ReplaceWithETagAsync(entity);

        Assert.NotNull(captured);
        Assert.Equal("\"abc\"", captured!.IfMatchEtag);
    }

    [Fact]
    public async Task ReplaceWithETagAsync_OnETagMismatch_ThrowsPreconditionFailed()
    {
        var (service, container) = BuildService();
        var entity = SampleEntity();

        container
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<DocumentOcrEntity>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("etag mismatch", HttpStatusCode.PreconditionFailed, 0, "", 0));

        var ex = await Assert.ThrowsAsync<CosmosException>(() => service.ReplaceWithETagAsync(entity));
        Assert.Equal(HttpStatusCode.PreconditionFailed, ex.StatusCode);
    }

    [Fact]
    public async Task GetByIdentifierAsync_WhenAbsent_ReturnsNull()
    {
        var (service, container) = BuildService();

        var iteratorMock = new Mock<FeedIterator<DocumentOcrEntity>>();
        iteratorMock.SetupSequence(i => i.HasMoreResults).Returns(false);

        container
            .Setup(c => c.GetItemQueryIterator<DocumentOcrEntity>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(iteratorMock.Object);

        var result = await service.GetByIdentifierAsync("TK-NONE");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdentifierAsync_WhenRecordExists_ReturnsIt()
    {
        var (service, container) = BuildService();
        var existing = SampleEntity();

        var feedResponseMock = new Mock<FeedResponse<DocumentOcrEntity>>();
        feedResponseMock
            .Setup(r => r.GetEnumerator())
            .Returns(new List<DocumentOcrEntity> { existing }.GetEnumerator());

        var iteratorMock = new Mock<FeedIterator<DocumentOcrEntity>>();
        iteratorMock.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);
        iteratorMock
            .Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResponseMock.Object);

        container
            .Setup(c => c.GetItemQueryIterator<DocumentOcrEntity>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(iteratorMock.Object);

        var result = await service.GetByIdentifierAsync("TK-1");

        Assert.NotNull(result);
        Assert.Equal("doc-1", result!.Id);
    }

    [Fact]
    public async Task GetByIdentifierAsync_BlankIdentifier_ReturnsNullWithoutQuery()
    {
        var (service, container) = BuildService();

        var result = await service.GetByIdentifierAsync(string.Empty);

        Assert.Null(result);
        container.Verify(
            c => c.GetItemQueryIterator<DocumentOcrEntity>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()),
            Times.Never);
    }
}
