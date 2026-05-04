using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using DocumentOcr.Common.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentOcr.UnitTests.Services;

/// <summary>
/// T034 — DocumentLockService behavior (FR-021..FR-024 + 24h stale rule).
/// </summary>
public class DocumentLockServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private const string Reviewer = "alice@contoso.com";
    private const string Other = "bob@contoso.com";

    private static (DocumentLockService service, Mock<ICosmosDbService> cosmos, DocumentOcrEntity entity)
        Build(Action<DocumentOcrEntity>? customize = null)
    {
        var entity = new DocumentOcrEntity
        {
            Id = "doc-1",
            Identifier = "TK-1",
        };
        customize?.Invoke(entity);

        var cosmos = new Mock<ICosmosDbService>();
        cosmos.Setup(c => c.GetDocumentByIdAsync("doc-1", "TK-1")).ReturnsAsync(entity);
        cosmos.Setup(c => c.ReplaceWithETagAsync(It.IsAny<DocumentOcrEntity>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((DocumentOcrEntity e, CancellationToken _) => e);

        var service = new DocumentLockService(cosmos.Object, NullLogger<DocumentLockService>.Instance, () => Now);
        return (service, cosmos, entity);
    }

    [Fact]
    public async Task TryCheckout_OnFreeDocument_Acquires()
    {
        var (service, _, _) = Build();

        var result = await service.TryCheckoutAsync("doc-1", "TK-1", Reviewer);

        Assert.True(result.Acquired);
        Assert.Equal(Reviewer, result.HeldBy);
        Assert.Equal(Now, result.HeldAt);
    }

    [Fact]
    public async Task TryCheckout_HeldByOtherWithinThreshold_Fails()
    {
        var (service, _, _) = Build(e =>
        {
            e.CheckedOutBy = Other;
            e.CheckedOutAt = Now.AddHours(-1);
        });

        var result = await service.TryCheckoutAsync("doc-1", "TK-1", Reviewer);

        Assert.False(result.Acquired);
        Assert.Equal(Other, result.HeldBy);
    }

    [Fact]
    public async Task TryCheckout_HeldByOtherStale_Acquires_AndLogs()
    {
        var (service, _, _) = Build(e =>
        {
            e.CheckedOutBy = Other;
            e.CheckedOutAt = Now.AddHours(-25);
        });

        var result = await service.TryCheckoutAsync("doc-1", "TK-1", Reviewer);

        Assert.True(result.Acquired);
        Assert.Equal(Reviewer, result.HeldBy);
    }

    [Fact]
    public async Task TryCheckout_HeldBySelf_RefreshesTimestamp()
    {
        var older = Now.AddHours(-2);
        var (service, _, _) = Build(e =>
        {
            e.CheckedOutBy = Reviewer;
            e.CheckedOutAt = older;
        });

        var result = await service.TryCheckoutAsync("doc-1", "TK-1", Reviewer);

        Assert.True(result.Acquired);
        Assert.Equal(Reviewer, result.HeldBy);
        Assert.Equal(Now, result.HeldAt);
    }

    [Fact]
    public async Task Checkin_ByHolder_StampsLastCheckedIn_AndClearsCheckout()
    {
        var (service, _, _) = Build(e =>
        {
            e.CheckedOutBy = Reviewer;
            e.CheckedOutAt = Now.AddMinutes(-5);
        });

        var result = await service.CheckinAsync("doc-1", "TK-1", Reviewer);

        Assert.Null(result.CheckedOutBy);
        Assert.Null(result.CheckedOutAt);
        Assert.Equal(Reviewer, result.LastCheckedInBy);
        Assert.Equal(Now, result.LastCheckedInAt);
    }

    [Fact]
    public async Task Checkin_ByNonHolder_Throws()
    {
        var (service, _, _) = Build(e =>
        {
            e.CheckedOutBy = Other;
            e.CheckedOutAt = Now.AddMinutes(-5);
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CheckinAsync("doc-1", "TK-1", Reviewer));
    }

    [Fact]
    public async Task CancelCheckout_DoesNotUpdateLastCheckedInStamps()
    {
        var earlier = Now.AddDays(-1);
        var (service, _, _) = Build(e =>
        {
            e.CheckedOutBy = Reviewer;
            e.CheckedOutAt = Now.AddMinutes(-5);
            e.LastCheckedInBy = "earlier@contoso.com";
            e.LastCheckedInAt = earlier;
        });

        var result = await service.CancelCheckoutAsync("doc-1", "TK-1", Reviewer);

        Assert.Null(result.CheckedOutBy);
        Assert.Null(result.CheckedOutAt);
        Assert.Equal("earlier@contoso.com", result.LastCheckedInBy);
        Assert.Equal(earlier, result.LastCheckedInAt);
    }

    // ---------- ETag retry coverage (ReplaceWithSingleRetry) ----------

    [Fact]
    public async Task TryCheckout_ETagConflict_RefetchesAndRetriesOnce()
    {
        var entity = new DocumentOcrEntity { Id = "doc-1", Identifier = "TK-1", ETag = "\"v1\"" };
        var fresh = new DocumentOcrEntity { Id = "doc-1", Identifier = "TK-1", ETag = "\"v2\"" };

        var cosmos = new Mock<ICosmosDbService>();
        cosmos.SetupSequence(c => c.GetDocumentByIdAsync("doc-1", "TK-1"))
              .ReturnsAsync(entity)
              .ReturnsAsync(fresh);
        cosmos.SetupSequence(c => c.ReplaceWithETagAsync(It.IsAny<DocumentOcrEntity>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new Microsoft.Azure.Cosmos.CosmosException("conflict", System.Net.HttpStatusCode.PreconditionFailed, 0, "", 0))
              .ReturnsAsync(fresh);

        var service = new DocumentLockService(cosmos.Object, NullLogger<DocumentLockService>.Instance, () => Now);

        var result = await service.TryCheckoutAsync("doc-1", "TK-1", Reviewer);

        Assert.True(result.Acquired);
        Assert.Equal(Reviewer, result.Document!.CheckedOutBy);
        cosmos.Verify(c => c.ReplaceWithETagAsync(It.IsAny<DocumentOcrEntity>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        cosmos.Verify(c => c.GetDocumentByIdAsync("doc-1", "TK-1"), Times.Exactly(2));
    }

    [Fact]
    public async Task TryCheckout_DocumentMissing_Throws()
    {
        var cosmos = new Mock<ICosmosDbService>();
        cosmos.Setup(c => c.GetDocumentByIdAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync((DocumentOcrEntity?)null);
        var service = new DocumentLockService(cosmos.Object, NullLogger<DocumentLockService>.Instance, () => Now);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TryCheckoutAsync("missing", "TK-x", Reviewer));
    }

    [Fact]
    public async Task TryCheckout_BlankReviewer_ThrowsArgumentException()
    {
        var (service, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TryCheckoutAsync("doc-1", "TK-1", " "));
    }

    [Fact]
    public async Task Checkin_HeldByOther_Throws()
    {
        var (service, _, _) = Build(e =>
        {
            e.CheckedOutBy = Other;
            e.CheckedOutAt = Now.AddMinutes(-5);
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CheckinAsync("doc-1", "TK-1", Reviewer));
    }

    [Fact]
    public async Task CancelCheckout_HeldByOther_Throws()
    {
        var (service, _, _) = Build(e =>
        {
            e.CheckedOutBy = Other;
            e.CheckedOutAt = Now.AddMinutes(-5);
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelCheckoutAsync("doc-1", "TK-1", Reviewer));
    }
}
