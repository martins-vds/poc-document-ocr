using Bunit;
using Bunit.TestDoubles;
using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using DocumentOcr.WebApp.Components.Pages;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DocumentOcr.IntegrationTests.WebApp.Pages;

/// <summary>
/// bUnit-driven full-render tests for the Documents listing page. Verifies
/// that the page issues a query to <see cref="ICosmosDbService"/>, surfaces
/// the loaded rows, and shows the empty-state message when none are returned.
/// </summary>
public sealed class DocumentsPageTests : IDisposable
{
    private readonly TestContext _ctx = new();
    private readonly Mock<ICosmosDbService> _cosmos = new();

    public DocumentsPageTests()
    {
        _ctx.Services.AddSingleton(_cosmos.Object);
        var auth = _ctx.AddTestAuthorization();
        auth.SetAuthorized("reviewer@example.com");
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void EmptyResults_ShowsEmptyStateMessage()
    {
        _cosmos.Setup(c => c.GetDocumentsAsync(It.IsAny<string?>(), It.IsAny<int?>()))
               .ReturnsAsync(new List<DocumentOcrEntity>());

        var cut = _ctx.RenderComponent<Documents>();

        cut.WaitForAssertion(() =>
            Assert.Contains("No documents found", cut.Markup));
    }

    [Fact]
    public void NonEmptyResults_RendersRows()
    {
        _cosmos.Setup(c => c.GetDocumentsAsync(It.IsAny<string?>(), It.IsAny<int?>()))
               .ReturnsAsync(new List<DocumentOcrEntity>
               {
                   new() { Id = "1", Identifier = "TK-1" },
                   new() { Id = "2", Identifier = "TK-2" },
               });

        var cut = _ctx.RenderComponent<Documents>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("TK-1", cut.Markup);
            Assert.Contains("TK-2", cut.Markup);
        });
    }
}
