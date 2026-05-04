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
/// T021a — FR-019 duplicate-identifier skip behaviour.
///
/// Targets the extracted <c>TrySkipDuplicateAsync</c> helper on
/// <c>PdfProcessorFunction</c>. The wider <c>Run</c> method is too coupled
/// to Azure SDK types (BlobContainerClient, QueueTrigger) for unit testing
/// without an integration host; the duplicate-skip pre-check is the only
/// part of FR-019 that is pure logic and is the part this test asserts.
/// </summary>
public class PdfProcessorFunctionTests
{
    private static PdfProcessorFunction BuildFunction(
        Mock<ICosmosDbService> cosmos,
        Mock<ILogger<PdfProcessorFunction>> logger)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new PdfProcessorFunction(
            logger.Object,
            Mock.Of<IPdfToImageService>(),
            Mock.Of<IDocumentIntelligenceService>(),
            Mock.Of<IDocumentAggregatorService>(),
            Mock.Of<IImageToPdfService>(),
            Mock.Of<IBlobStorageService>(),
            cosmos.Object,
            Mock.Of<IOperationService>(),
            Mock.Of<IDocumentSchemaMapperService>(),
            config);
    }

    private static AggregatedDocument BuildAggregated(string identifier) => new()
    {
        Identifier = identifier,
        Pages = new List<PageOcrResult>(),
        PageProvenance = new List<PageProvenanceEntry>(),
    };

    [Fact]
    public async Task TrySkipDuplicateAsync_WhenIdentifierExists_AddsToSkippedAndReturnsTrue()
    {
        var cosmos = new Mock<ICosmosDbService>();
        cosmos.Setup(c => c.GetByIdentifierAsync("dup-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentOcrEntity { Id = "existing-id", Identifier = "dup-123" });
        var logger = new Mock<ILogger<PdfProcessorFunction>>();
        var function = BuildFunction(cosmos, logger);
        var result = new ProcessingResult();

        var skipped = await function.TrySkipDuplicateAsync(BuildAggregated("dup-123"), result, "op-1");

        Assert.True(skipped);
        Assert.Contains("dup-123", result.SkippedDuplicateIdentifiers);
    }

    [Fact]
    public async Task TrySkipDuplicateAsync_WhenIdentifierExists_LogsWarningWithOperationAndIdentifier()
    {
        var cosmos = new Mock<ICosmosDbService>();
        cosmos.Setup(c => c.GetByIdentifierAsync("dup-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentOcrEntity { Id = "existing-id", Identifier = "dup-456" });
        var logger = new Mock<ILogger<PdfProcessorFunction>>();
        var function = BuildFunction(cosmos, logger);

        await function.TrySkipDuplicateAsync(BuildAggregated("dup-456"), new ProcessingResult(), "op-42");

        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("FR-019")
                                          && v.ToString()!.Contains("op-42")
                                          && v.ToString()!.Contains("dup-456")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TrySkipDuplicateAsync_WhenIdentifierAbsent_ReturnsFalseAndDoesNotMutateResult()
    {
        var cosmos = new Mock<ICosmosDbService>();
        cosmos.Setup(c => c.GetByIdentifierAsync("new-789", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentOcrEntity?)null);
        var logger = new Mock<ILogger<PdfProcessorFunction>>();
        var function = BuildFunction(cosmos, logger);
        var result = new ProcessingResult();

        var skipped = await function.TrySkipDuplicateAsync(BuildAggregated("new-789"), result, "op-1");

        Assert.False(skipped);
        Assert.Empty(result.SkippedDuplicateIdentifiers);
    }
}
