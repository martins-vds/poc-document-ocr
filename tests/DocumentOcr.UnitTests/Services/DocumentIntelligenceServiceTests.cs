using Azure.AI.FormRecognizer.DocumentAnalysis;
using DocumentOcr.Processor.Services;

namespace DocumentOcr.UnitTests.Services;

/// <summary>
/// T033 — <see cref="DocumentIntelligenceService.MapSignatureValue"/>
/// covers the SDK's quirk of returning <c>null</c> or a strongly-typed
/// <c>DocumentSignatureType</c> for signature fields. The mapper expects
/// a plain string ("signed" / "unsigned" / "present") downstream.
/// </summary>
public class DocumentIntelligenceServiceTests
{
    [Fact]
    public void MapSignatureValue_WhenSdkReturnsSigned_ReturnsSigned()
    {
        var value = DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType("signed");
        var field = DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.Signature, value, content: "signed",
            boundingRegions: Array.Empty<BoundingRegion>(),
            spans: Array.Empty<DocumentSpan>(),
            confidence: 0.9f);

        var result = DocumentIntelligenceService.MapSignatureValue(field);

        Assert.Equal("signed", result);
    }

    [Fact]
    public void MapSignatureValue_WhenSdkReturnsUnsigned_ReturnsUnsigned()
    {
        var value = DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType("unsigned");
        var field = DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.Signature, value, content: "unsigned",
            boundingRegions: Array.Empty<BoundingRegion>(),
            spans: Array.Empty<DocumentSpan>(),
            confidence: 0.9f);

        var result = DocumentIntelligenceService.MapSignatureValue(field);

        Assert.Equal("unsigned", result);
    }

    [Fact]
    public void MapSignatureValue_WhenAsStringThrows_FallsBackToPresent()
    {
        // DocumentSignatureType-typed value will throw InvalidCastException
        // when AsString() is invoked — exercises the catch branch.
        var value = DocumentAnalysisModelFactory.DocumentFieldValueWithSignatureFieldType(DocumentSignatureType.Signed);
        var field = DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.Signature, value, content: null,
            boundingRegions: Array.Empty<BoundingRegion>(),
            spans: Array.Empty<DocumentSpan>(),
            confidence: 0.9f);

        var result = DocumentIntelligenceService.MapSignatureValue(field);

        Assert.Equal("present", result);
    }

    [Fact]
    public void MapSignatureValue_WhenStringIsEmpty_FallsBackToPresent()
    {
        var value = DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType("");
        var field = DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.Signature, value, content: "",
            boundingRegions: Array.Empty<BoundingRegion>(),
            spans: Array.Empty<DocumentSpan>(),
            confidence: 0.9f);

        var result = DocumentIntelligenceService.MapSignatureValue(field);

        Assert.Equal("present", result);
    }
}
