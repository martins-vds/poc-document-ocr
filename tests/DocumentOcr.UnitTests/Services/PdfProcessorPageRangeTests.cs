using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using DocumentOcr.Processor.Functions;
using DocumentOcr.Processor.Models;
using DocumentOcr.Processor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentOcr.UnitTests.Services;

/// <summary>
/// T010 — verifies the page-range selection behaviour added to
/// <see cref="PdfProcessorFunction"/> by feature
/// 002-upload-page-range-selection. Tests target the extracted
/// <c>RunOcrLoopAsync</c> helper because the wider <c>Run</c> method is too
/// coupled to Azure SDK types for unit testing (same constraint as
/// <see cref="PdfProcessorFunctionTests"/>).
///
/// Covers FR-009 (only selected pages sent to OCR), FR-011 / SC-006
/// (per-document <c>pageNumber</c> values are 1..N within the selected subset,
/// not the original PDF page numbers), and the bounds-error failure path.
/// </summary>
public class PdfProcessorPageRangeTests
{
    private static MemoryStream Page(byte tag) => new(new[] { tag, tag, tag });

    private static (PdfProcessorFunction func, Mock<IDocumentIntelligenceService> di, Mock<IOperationService> ops, List<Stream> streams) Build(int pageCount)
    {
        var di = new Mock<IDocumentIntelligenceService>();
        di.Setup(d => d.AnalyzeDocumentAsync(It.IsAny<Stream>()))
          .ReturnsAsync(new Dictionary<string, object>());

        var ops = new Mock<IOperationService>();
        // The cancellation-check polls every iteration; return non-cancelled.
        ops.Setup(o => o.GetOperationAsync(It.IsAny<string>()))
           .ReturnsAsync((string id) => new Operation { Id = id, CancelRequested = false });
        ops.Setup(o => o.UpdateOperationAsync(It.IsAny<Operation>()))
           .ReturnsAsync((Operation o) => o);

        var streams = Enumerable.Range(1, pageCount).Select(i => (Stream)Page((byte)i)).ToList();

        var function = new PdfProcessorFunction(
            Mock.Of<ILogger<PdfProcessorFunction>>(),
            Mock.Of<IPdfToImageService>(),
            di.Object,
            Mock.Of<IDocumentAggregatorService>(),
            Mock.Of<IImageToPdfService>(),
            Mock.Of<IBlobStorageService>(),
            Mock.Of<ICosmosDbService>(),
            ops.Object,
            Mock.Of<IDocumentSchemaMapperService>(),
            new ConfigurationBuilder().AddInMemoryCollection().Build());

        return (function, di, ops, streams);
    }

    [Fact]
    public async Task RunOcrLoopAsync_PageRangeNull_AnalyzesAllPages()
    {
        var (func, di, _, streams) = Build(20);
        var op = new Operation { Id = "op-1" };

        var result = await func.RunOcrLoopAsync(streams, pageRange: null, op);

        Assert.NotNull(result);
        Assert.Equal(20, result!.Count);
        di.Verify(d => d.AnalyzeDocumentAsync(It.IsAny<Stream>()), Times.Exactly(20));
        // Default (all pages) path: operation should NOT be marked Failed.
        Assert.NotEqual(OperationStatus.Failed, op.Status);
    }

    [Fact]
    public async Task RunOcrLoopAsync_RestrictedRange_AnalyzesOnlySelectedStreams()
    {
        var (func, di, _, streams) = Build(20);
        var op = new Operation { Id = "op-2" };

        // Capture which streams were passed to OCR, in call order.
        var captured = new List<Stream>();
        di.Setup(d => d.AnalyzeDocumentAsync(It.IsAny<Stream>()))
          .Callback<Stream>(s => captured.Add(s))
          .ReturnsAsync(new Dictionary<string, object>());

        var result = await func.RunOcrLoopAsync(streams, pageRange: "3-12, 15", op);

        // FR-009: exactly 11 OCR calls.
        Assert.NotNull(result);
        Assert.Equal(11, result!.Count);
        di.Verify(d => d.AnalyzeDocumentAsync(It.IsAny<Stream>()), Times.Exactly(11));

        // The worker MUST index the existing imageStreams list by selectedPage-1
        // (no re-decode). Stream identity proves that.
        var expectedSourcePages = new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 15 };
        for (var i = 0; i < expectedSourcePages.Length; i++)
        {
            Assert.Same(streams[expectedSourcePages[i] - 1], captured[i]);
        }
    }

    [Fact]
    public async Task RunOcrLoopAsync_RestrictedRange_AssignsDocumentLocal1ToN_PageNumbers()
    {
        // FR-011 / SC-006 invariant: pageNumber values forwarded to PageOcrResult
        // are 1..11 (document-local), NOT the original PDF page numbers 3..15.
        var (func, _, _, streams) = Build(20);
        var op = new Operation { Id = "op-3" };

        var result = await func.RunOcrLoopAsync(streams, pageRange: "3-12, 15", op);

        Assert.NotNull(result);
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            result!.Select(r => r.PageNumber).ToArray());
    }

    [Fact]
    public async Task RunOcrLoopAsync_OutOfBoundsRange_FailsOperation_AndDoesNotCallOcr()
    {
        var (func, di, ops, streams) = Build(20);
        var op = new Operation { Id = "op-4" };

        var result = await func.RunOcrLoopAsync(streams, pageRange: "25", op);

        Assert.Null(result);
        Assert.Equal(OperationStatus.Failed, op.Status);
        Assert.NotNull(op.Error);
        Assert.Contains("25", op.Error);
        Assert.Contains("20", op.Error);
        di.Verify(d => d.AnalyzeDocumentAsync(It.IsAny<Stream>()), Times.Never);
        ops.Verify(o => o.UpdateOperationAsync(It.Is<Operation>(o => o.Status == OperationStatus.Failed)), Times.Once);
    }

    [Fact]
    public async Task RunOcrLoopAsync_RestrictedRange_DisposesExcludedStreamsUpFront()
    {
        var (func, _, _, streams) = Build(5);
        var op = new Operation { Id = "op-5" };

        await func.RunOcrLoopAsync(streams, pageRange: "2,4", op);

        // Streams 1, 3, 5 (1-indexed) are excluded → indexes 0, 2, 4 disposed.
        Assert.Throws<ObjectDisposedException>(() => streams[0].Read(new byte[1], 0, 1));
        Assert.Throws<ObjectDisposedException>(() => streams[2].Read(new byte[1], 0, 1));
        Assert.Throws<ObjectDisposedException>(() => streams[4].Read(new byte[1], 0, 1));
        // Selected streams must still be open (the caller disposes them later).
        Assert.True(streams[1].CanRead);
        Assert.True(streams[3].CanRead);
    }
}
