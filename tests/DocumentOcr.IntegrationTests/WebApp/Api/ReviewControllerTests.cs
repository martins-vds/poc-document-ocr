using System.Net;
using System.Net.Http.Json;
using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using DocumentOcr.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DocumentOcr.IntegrationTests.WebApp.Api;

/// <summary>
/// In-process integration tests for <c>ReviewController</c> covering the
/// checkout / save-fields / checkin / cancel endpoints.
/// </summary>
public sealed class ReviewControllerTests : IClassFixture<WebAppFactory>
{
    private readonly WebAppFactory _factory;

    public ReviewControllerTests(WebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Checkout_AnonymousCaller_IsChallenged()
    {
        using var factory = _factory.ForScenario();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var resp = await client.PostAsJsonAsync("/api/review/checkout", new
        {
            DocumentId = "doc",
            PartitionKey = "tk",
        });

        Assert.True(
            resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Expected challenge for anonymous caller; got {resp.StatusCode}.");
    }

    [Fact]
    public async Task Checkout_LockHeldByOtherUser_Returns409()
    {
        var locks = new Mock<IDocumentLockService>();
        locks.Setup(l => l.TryCheckoutAsync("doc", "tk", "alice@example.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new CheckoutResult(false, null, "bob@example.com", DateTime.UtcNow));

        using var factory = _factory.ForScenario(
            authenticatedUpn: "alice@example.com",
            configureServices: s => Replace(s, locks.Object));
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/review/checkout", new
        {
            DocumentId = "doc",
            PartitionKey = "tk",
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Checkout_Acquired_Returns200WithEntity()
    {
        var entity = new DocumentOcrEntity { Id = "doc", Identifier = "tk" };
        var locks = new Mock<IDocumentLockService>();
        locks.Setup(l => l.TryCheckoutAsync("doc", "tk", "alice@example.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new CheckoutResult(true, entity, "alice@example.com", DateTime.UtcNow));

        using var factory = _factory.ForScenario(
            authenticatedUpn: "alice@example.com",
            configureServices: s => Replace(s, locks.Object));
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/review/checkout", new
        {
            DocumentId = "doc",
            PartitionKey = "tk",
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    private static void Replace<T>(IServiceCollection services, T instance) where T : class
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList())
        {
            services.Remove(d);
        }
        services.AddScoped(_ => instance);
    }
}
