using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using DocumentOcr.Processor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentOcr.UnitTests.Services;

public class DocumentIntelligenceServiceAnalyzeTests
{
    [Fact]
    public void Constructor_NullClient_Throws()
    {
        var logger = new Mock<ILogger<DocumentIntelligenceService>>();
        Assert.Throws<ArgumentNullException>(() =>
            new DocumentIntelligenceService((DocumentAnalysisClient)null!, logger.Object));
    }

    [Fact]
    public void CreateClient_MissingEndpoint_Throws()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["DocumentIntelligence:Endpoint"]).Returns((string?)null);
        Assert.Throws<InvalidOperationException>(() => DocumentIntelligenceService.CreateClient(config.Object));
    }

    [Fact]
    public void CreateClient_WithEndpoint_ReturnsClient()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["DocumentIntelligence:Endpoint"]).Returns("https://di.example.com/");
        var client = DocumentIntelligenceService.CreateClient(config.Object);
        Assert.NotNull(client);
    }

    private static AnalyzeResult MakeResult(IEnumerable<AnalyzedDocument>? documents = null, int pageCount = 1)
    {
        var pages = Enumerable.Range(1, pageCount).Select(i =>
            DocumentAnalysisModelFactory.DocumentPage(
                pageNumber: i, angle: 0f, width: 8.5f, height: 11f,
                unit: DocumentPageLengthUnit.Inch,
                spans: Array.Empty<DocumentSpan>(),
                words: Array.Empty<DocumentWord>(),
                selectionMarks: Array.Empty<DocumentSelectionMark>(),
                lines: Array.Empty<DocumentLine>())).ToArray();

        return DocumentAnalysisModelFactory.AnalyzeResult(
            modelId: "prebuilt-document",
            content: "",
            pages: pages,
            paragraphs: Array.Empty<DocumentParagraph>(),
            tables: Array.Empty<DocumentTable>(),
            keyValuePairs: Array.Empty<DocumentKeyValuePair>(),
            styles: Array.Empty<DocumentStyle>(),
            languages: Array.Empty<DocumentLanguage>(),
            documents: documents ?? Array.Empty<AnalyzedDocument>());
    }

    private static Mock<DocumentAnalysisClient> MakeClient(AnalyzeResult result)
    {
        var op = new Mock<AnalyzeDocumentOperation>();
        op.SetupGet(o => o.Value).Returns(result);
        op.SetupGet(o => o.HasValue).Returns(true);

        var client = new Mock<DocumentAnalysisClient>();
        client.Setup(c => c.AnalyzeDocumentAsync(
            It.IsAny<WaitUntil>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<AnalyzeDocumentOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(op.Object);
        return client;
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_NoDocuments_ReturnsPageCountOnly()
    {
        var result = MakeResult(pageCount: 2);
        var client = MakeClient(result);

        var svc = new DocumentIntelligenceService(client.Object, new Mock<ILogger<DocumentIntelligenceService>>().Object);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var data = await svc.AnalyzeDocumentAsync(stream);

        Assert.Equal(2, data["PageCount"]);
        Assert.False(data.ContainsKey("Fields"));
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_WithStringField_ExtractsValueString()
    {
        var value = DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType("hello");
        var field = DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.String, value, "hello",
            Array.Empty<BoundingRegion>(), Array.Empty<DocumentSpan>(), 0.95f);
        var doc = DocumentAnalysisModelFactory.AnalyzedDocument(
            documentType: "test", boundingRegions: Array.Empty<BoundingRegion>(),
            spans: Array.Empty<DocumentSpan>(),
            fields: new Dictionary<string, DocumentField> { ["Name"] = field },
            confidence: 0.9f);

        var client = MakeClient(MakeResult(new[] { doc }));
        var svc = new DocumentIntelligenceService(client.Object, new Mock<ILogger<DocumentIntelligenceService>>().Object);

        using var stream = new MemoryStream(new byte[] { 1 });
        var data = await svc.AnalyzeDocumentAsync(stream);

        var fields = Assert.IsType<Dictionary<string, object>>(data["Fields"]);
        var name = Assert.IsType<Dictionary<string, object>>(fields["Name"]);
        Assert.Equal("hello", name["valueString"]);
        Assert.Equal("String", name["type"]);
        Assert.Equal(0.95f, name["confidence"]);
        Assert.Equal("hello", name["content"]);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_WithSignatureField_ExtractsValueSignature()
    {
        var value = DocumentAnalysisModelFactory.DocumentFieldValueWithSignatureFieldType(DocumentSignatureType.Signed);
        var field = DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.Signature, value, content: null,
            Array.Empty<BoundingRegion>(), Array.Empty<DocumentSpan>(), 0.7f);
        var doc = DocumentAnalysisModelFactory.AnalyzedDocument(
            documentType: "test", boundingRegions: Array.Empty<BoundingRegion>(),
            spans: Array.Empty<DocumentSpan>(),
            fields: new Dictionary<string, DocumentField> { ["Sig"] = field },
            confidence: 0.9f);

        var client = MakeClient(MakeResult(new[] { doc }));
        var svc = new DocumentIntelligenceService(client.Object, new Mock<ILogger<DocumentIntelligenceService>>().Object);

        using var stream = new MemoryStream(new byte[] { 1 });
        var data = await svc.AnalyzeDocumentAsync(stream);

        var fields = Assert.IsType<Dictionary<string, object>>(data["Fields"]);
        var sig = Assert.IsType<Dictionary<string, object>>(fields["Sig"]);
        Assert.Equal("present", sig["valueSignature"]);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_ClientThrows_PropagatesException()
    {
        var client = new Mock<DocumentAnalysisClient>();
        client.Setup(c => c.AnalyzeDocumentAsync(
            It.IsAny<WaitUntil>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<AnalyzeDocumentOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var svc = new DocumentIntelligenceService(client.Object, new Mock<ILogger<DocumentIntelligenceService>>().Object);

        using var stream = new MemoryStream(new byte[] { 1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AnalyzeDocumentAsync(stream));
    }
}
