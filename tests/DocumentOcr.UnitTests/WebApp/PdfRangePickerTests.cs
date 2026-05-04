using Bunit;
using DocumentOcr.WebApp.Components.Shared;
using DocumentOcr.WebApp.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace DocumentOcr.UnitTests.WebApp;

/// <summary>
/// US2 (T020) — bUnit tests for the in-page range picker. Verifies the
/// validation summary, error visibility, and OnEntryChanged round-trip.
/// JS interop is faked via <see cref="BunitJSInterop"/>: the
/// <c>range-picker.js</c> module is intercepted so the test process never
/// touches a real browser or pdf.js.
/// </summary>
public class PdfRangePickerTests : IDisposable
{
    private readonly TestContext _ctx = new();

    public PdfRangePickerTests()
    {
        // Intercept the dynamic import('./range-picker.js') and the methods
        // PdfRangePicker invokes on the returned module reference.
        var module = _ctx.JSInterop.SetupModule("/lib/pdfjs/range-picker.js");
        module.Setup<PdfRangePicker.LoadResult>("loadDocument", _ => true)
              .SetResult(new PdfRangePicker.LoadResult { RendererId = 1, NumPages = 20 });
        module.SetupVoid("renderPage", _ => true).SetVoidResult();
        module.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public void Dispose() => _ctx.Dispose();

    private static IBrowserFile FakeFile(string name = "doc.pdf", long size = 12_345)
        => new FakeBrowserFile(name, size);

    [Fact]
    public void OutOfBoundsRange_ShowsError_AndRaisesOnEntryChangedWithRangeError()
    {
        var entry = new UploadFileEntry { File = FakeFile() };
        UploadFileEntry? lastSeen = null;

        var cut = _ctx.RenderComponent<PdfRangePicker>(p => p
            .Add(c => c.Entry, entry)
            .Add(c => c.OnEntryChanged, EventCallback.Factory.Create<UploadFileEntry>(this, e => lastSeen = e)));

        // After OnAfterRenderAsync the JS load fakery resolves; numPages = 20.
        cut.WaitForAssertion(() => Assert.Equal(20, entry.TotalPages));

        // Type an out-of-bounds expression.
        var input = cut.Find("input[type=text]");
        input.Input("25-30");

        Assert.NotNull(entry.RangeError);
        Assert.NotNull(lastSeen);
        Assert.NotNull(lastSeen!.RangeError);
        // Error is rendered in the picker's error <small>.
        var error = cut.Find("small.range-picker-error");
        Assert.False(string.IsNullOrWhiteSpace(error.TextContent));
        Assert.Equal(entry.RangeError, error.TextContent);
    }

    [Fact]
    public void ValidExpression_ShowsSummary_With11Pages()
    {
        var entry = new UploadFileEntry { File = FakeFile() };

        var cut = _ctx.RenderComponent<PdfRangePicker>(p => p
            .Add(c => c.Entry, entry)
            .Add(c => c.OnEntryChanged, EventCallback.Factory.Create<UploadFileEntry>(this, _ => { })));

        cut.WaitForAssertion(() => Assert.Equal(20, entry.TotalPages));

        cut.Find("input[type=text]").Input("3-12, 15");

        Assert.Null(entry.RangeError);
        Assert.NotNull(entry.Selection);
        Assert.Equal(11, entry.Selection!.Pages.Count);

        var summary = cut.Find("small.range-picker-summary");
        Assert.Contains("11", summary.TextContent);
        Assert.Contains("3", summary.TextContent);
        Assert.Contains("15", summary.TextContent);
    }

    [Fact]
    public void Whitespace_IsTreatedAsAllPages_NoError()
    {
        var entry = new UploadFileEntry { File = FakeFile() };

        var cut = _ctx.RenderComponent<PdfRangePicker>(p => p
            .Add(c => c.Entry, entry)
            .Add(c => c.OnEntryChanged, EventCallback.Factory.Create<UploadFileEntry>(this, _ => { })));

        cut.WaitForAssertion(() => Assert.Equal(20, entry.TotalPages));

        cut.Find("input[type=text]").Input("   ");

        Assert.Null(entry.RangeError);
        Assert.NotNull(entry.Selection);
        Assert.True(entry.Selection!.IsAllPages);
        Assert.Contains("All pages", cut.Find("small.range-picker-summary").TextContent);
    }

    private sealed class FakeBrowserFile : IBrowserFile
    {
        public FakeBrowserFile(string name, long size)
        {
            Name = name;
            Size = size;
            LastModified = DateTimeOffset.UtcNow;
            ContentType = "application/pdf";
        }
        public string Name { get; }
        public DateTimeOffset LastModified { get; }
        public long Size { get; }
        public string ContentType { get; }
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"
    }
}
