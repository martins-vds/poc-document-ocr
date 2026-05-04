using Bunit;
using Bunit.TestDoubles;
using DocumentOcr.Common.Interfaces;
using DocumentOcr.WebApp.Components.Pages;
using DocumentOcr.WebApp.Models;
using DocumentOcr.WebApp.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace DocumentOcr.IntegrationTests.WebApp.Pages;

/// <summary>
/// bUnit-driven full-render tests for the Upload page. Validates the page
/// renders for an authorized user and that the submit button is disabled on
/// initial render (no files selected). Heavy upload-flow logic is exercised
/// in the unit-tests project (<c>UploadPageGatingTests</c>).
/// </summary>
public sealed class UploadPageTests : IDisposable
{
    private readonly TestContext _ctx = new();

    public UploadPageTests()
    {
        _ctx.Services.AddSingleton(new Mock<IOperationsApiService>().Object);
        _ctx.Services.AddSingleton(new Mock<IBlobStorageService>().Object);
        _ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Upload:ContainerName"] = "uploaded-pdfs",
            }).Build());

        // Required by <PdfRangePicker>'s dynamic JS import even when no file
        // has been selected yet (the page only renders pickers when files
        // exist, but bUnit still needs JS interop registered).
        var module = _ctx.JSInterop.SetupModule("/lib/pdfjs/range-picker.js");
        module.Setup<PdfRangePicker_LoadResultProxy>("loadDocument", _ => true)
              .SetResult(new PdfRangePicker_LoadResultProxy { RendererId = 1, NumPages = 1 });
        module.SetupVoid("renderPage", _ => true).SetVoidResult();
        module.SetupVoid("dispose", _ => true).SetVoidResult();

        // Authorize the page render via bUnit's fake authentication.
        var authCtx = _ctx.AddTestAuthorization();
        authCtx.SetAuthorized("reviewer@example.com");
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void RendersForAuthorizedUser_AndSubmitIsDisabledWhenNoFilesSelected()
    {
        var cut = _ctx.RenderComponent<Upload>();

        Assert.Contains("Upload Documents", cut.Markup);
        var submit = cut.Find("button.btn-primary");
        Assert.True(submit.HasAttribute("disabled"));
    }

    // Local proxy mirrors the shape of PdfRangePicker.LoadResult so we can
    // satisfy bUnit's typed JS-module setup without exposing the internal
    // record from production code.
    public sealed class PdfRangePicker_LoadResultProxy
    {
        public int RendererId { get; set; }
        public int NumPages { get; set; }
    }
}
