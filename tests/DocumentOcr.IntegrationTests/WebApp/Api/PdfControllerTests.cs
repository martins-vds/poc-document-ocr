using System.Net;
using System.Net.Http.Headers;
using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using DocumentOcr.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DocumentOcr.IntegrationTests.WebApp.Api;

/// <summary>
/// In-process integration tests for <c>PdfController</c>. Hosts the real
/// WebApp via <see cref="WebAppFactory"/> and replaces the blob/cosmos
/// services with Moq-backed test doubles.
/// </summary>
public sealed class PdfControllerTests : IClassFixture<WebAppFactory>
{
    private readonly WebAppFactory _factory;

    public PdfControllerTests(WebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPdf_AnonymousCaller_RedirectsOrUnauthorized()
    {
        using var factory = _factory.ForScenario();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/pdf/doc-1/TK-1");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Expected challenge for anonymous caller; got {response.StatusCode}.");
    }

    [Fact]
    public async Task GetPdf_AuthenticatedCaller_ReturnsBlobStream()
    {
        var blob = new Mock<IBlobStorageService>();
        var cosmos = new Mock<ICosmosDbService>();

        cosmos.Setup(c => c.GetDocumentByIdAsync("doc-1", "TK-1"))
              .ReturnsAsync(new DocumentOcrEntity
              {
                  Id = "doc-1",
                  Identifier = "TK-1",
                  ContainerName = "processed-documents",
                  BlobName = "doc-1.pdf",
              });

        blob.Setup(b => b.DownloadBlobAsync("processed-documents", "doc-1.pdf"))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF

        using var factory = _factory.ForScenario(
            authenticatedUpn: "reviewer@example.com",
            configureServices: s =>
            {
                Replace(s, blob.Object);
                Replace(s, cosmos.Object);
            });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/pdf/doc-1/TK-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new MediaTypeHeaderValue("application/pdf"), response.Content.Headers.ContentType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, bytes);
    }

    [Fact]
    public async Task GetPdf_DocumentMissing_ReturnsNotFound()
    {
        var blob = new Mock<IBlobStorageService>();
        var cosmos = new Mock<ICosmosDbService>();
        cosmos.Setup(c => c.GetDocumentByIdAsync("missing", "x"))
              .ReturnsAsync((DocumentOcrEntity?)null);

        using var factory = _factory.ForScenario(
            authenticatedUpn: "reviewer@example.com",
            configureServices: s =>
            {
                Replace(s, blob.Object);
                Replace(s, cosmos.Object);
            });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/pdf/missing/x");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
