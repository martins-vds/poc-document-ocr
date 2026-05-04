using DocumentOcr.WebApp.Models;
using DocumentOcr.WebApp.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace DocumentOcr.UnitTests.WebApp;

/// <summary>
/// US2 (T021) — verifies the Upload page submit-gating rule. The rule lives
/// in <see cref="UploadGating"/> so it can be exercised without hosting the
/// full Blazor renderer (which would require an authenticated user and a
/// real <c>InputFile</c> harness).
/// </summary>
public class UploadPageGatingTests
{
    private static UploadFileEntry MakeEntry(string name, int? totalPages, string? rangeError)
    {
        return new UploadFileEntry
        {
            File = new FakeBrowserFile(name),
            TotalPages = totalPages,
            RangeError = rangeError,
        };
    }

    [Fact]
    public void CanSubmit_FalseWhenAnyEntryHasRangeError()
    {
        var entries = new List<UploadFileEntry>
        {
            MakeEntry("a.pdf", 20, null),
            MakeEntry("b.pdf", 15, "Page 25 exceeds total of 15"),
        };
        Assert.False(UploadGating.CanSubmit(entries));
        Assert.Contains("b.pdf", UploadGating.BlockedFileNames(entries));
        Assert.DoesNotContain("a.pdf", UploadGating.BlockedFileNames(entries));
    }

    [Fact]
    public void CanSubmit_TrueWhenErrorClearedFromAllEntries()
    {
        var entries = new List<UploadFileEntry>
        {
            MakeEntry("a.pdf", 20, null),
            MakeEntry("b.pdf", 15, "bad"),
        };
        Assert.False(UploadGating.CanSubmit(entries));

        // Reviewer corrects the range; error is cleared.
        entries[1].RangeError = null;

        Assert.True(UploadGating.CanSubmit(entries));
        Assert.Empty(UploadGating.BlockedFileNames(entries));
    }

    [Fact]
    public void CanSubmit_FalseWhenPreviewIncomplete()
    {
        // Entry has not yet finished its pdf.js probe → TotalPages is null.
        var entries = new List<UploadFileEntry>
        {
            MakeEntry("a.pdf", null, null),
        };
        Assert.False(UploadGating.CanSubmit(entries));
    }

    [Fact]
    public void CanSubmit_FalseWhenNoFilesSelected()
    {
        Assert.False(UploadGating.CanSubmit(new List<UploadFileEntry>()));
    }

    private sealed class FakeBrowserFile : IBrowserFile
    {
        public FakeBrowserFile(string name) { Name = name; }
        public string Name { get; }
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 100;
        public string ContentType => "application/pdf";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new MemoryStream();
    }
}
