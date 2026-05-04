using System.Net;
using DocumentOcr.Common.Models;
using DocumentOcr.Common.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentOcr.UnitTests.Services;

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
        containerMock.SetupGet(c => c.Id).Returns("ProcessedDocuments");
        var dbMock = new Mock<Database>();
        dbMock.SetupGet(d => d.Id).Returns("DocumentOcrDb");
        containerMock.SetupGet(c => c.Database).Returns(dbMock.Object);

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

    // ---------- Coverage extensions ----------

    private static Mock<ItemResponse<DocumentOcrEntity>> ItemResponse(DocumentOcrEntity entity)
    {
        var m = new Mock<ItemResponse<DocumentOcrEntity>>();
        m.SetupGet(r => r.Resource).Returns(entity);
        return m;
    }

    [Fact]
    public async Task CreateDocumentAsync_PersistsAndReturnsResource()
    {
        var (service, container) = BuildService();
        var entity = SampleEntity();
        container
            .Setup(c => c.CreateItemAsync(
                entity, It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemResponse(entity).Object);

        var result = await service.CreateDocumentAsync(entity);

        Assert.Same(entity, result);
    }

    [Fact]
    public async Task CreateDocumentAsync_ContainerMissing_RethrowsAsInvalidOperation()
    {
        var (service, container) = BuildService();
        container
            .Setup(c => c.CreateItemAsync(
                It.IsAny<DocumentOcrEntity>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("missing", HttpStatusCode.NotFound, 0, "", 0));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDocumentAsync(SampleEntity()));
    }

    [Fact]
    public async Task UpdateDocumentAsync_ReplacesItem()
    {
        var (service, container) = BuildService();
        var entity = SampleEntity();
        container
            .Setup(c => c.ReplaceItemAsync(
                entity, entity.Id, It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemResponse(entity).Object);

        var result = await service.UpdateDocumentAsync(entity);

        Assert.Same(entity, result);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_WhenAbsent_ReturnsNull()
    {
        var (service, container) = BuildService();
        container
            .Setup(c => c.ReadItemAsync<DocumentOcrEntity>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("missing", HttpStatusCode.NotFound, 0, "", 0));

        var result = await service.GetDocumentByIdAsync("doc-x", "TK-x");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_WhenPresent_ReturnsEntity()
    {
        var (service, container) = BuildService();
        var entity = SampleEntity();
        container
            .Setup(c => c.ReadItemAsync<DocumentOcrEntity>(
                "doc-1", It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItemResponse(entity).Object);

        var result = await service.GetDocumentByIdAsync("doc-1", "TK-1");

        Assert.NotNull(result);
        Assert.Equal("doc-1", result!.Id);
    }

    [Fact]
    public async Task GetDocumentsAsync_WithReviewStatusFilter_ParameterisesQuery()
    {
        var (service, container) = BuildService();
        var existing = SampleEntity();

        var feed = new Mock<FeedResponse<DocumentOcrEntity>>();
        feed.Setup(r => r.GetEnumerator()).Returns(new List<DocumentOcrEntity> { existing }.GetEnumerator());
        var iter = new Mock<FeedIterator<DocumentOcrEntity>>();
        iter.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);
        iter.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(feed.Object);

        QueryDefinition? capturedQuery = null;
        container
            .Setup(c => c.GetItemQueryIterator<DocumentOcrEntity>(
                It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((q, _, _) => capturedQuery = q)
            .Returns(iter.Object);

        var result = await service.GetDocumentsAsync(reviewStatus: "Reviewed", maxItems: 10);

        Assert.Single(result);
        Assert.NotNull(capturedQuery);
        Assert.Contains("@reviewStatus", capturedQuery!.QueryText);
    }

    [Fact]
    public async Task GetDocumentsAsync_WithoutFilter_OmitsWhereClause()
    {
        var (service, container) = BuildService();
        var iter = new Mock<FeedIterator<DocumentOcrEntity>>();
        iter.SetupSequence(i => i.HasMoreResults).Returns(false);

        QueryDefinition? capturedQuery = null;
        container
            .Setup(c => c.GetItemQueryIterator<DocumentOcrEntity>(
                It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((q, _, _) => capturedQuery = q)
            .Returns(iter.Object);

        await service.GetDocumentsAsync();

        Assert.NotNull(capturedQuery);
        Assert.DoesNotContain("WHERE", capturedQuery!.QueryText);
    }
}
