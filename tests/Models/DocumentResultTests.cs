using DocumentOcr.Processor.Models;
using Xunit;

namespace DocumentOcr.Tests.Models;

public class DocumentResultTests
{
    [Fact]
    public void DocumentResult_DefaultValues_AreInitialized()
    {
        // Arrange & Act
        var result = new DocumentResult();
        
        // Assert
        Assert.Equal(0, result.DocumentNumber);
        Assert.Equal(0, result.PageCount);
        Assert.NotNull(result.PageNumbers);
        Assert.Empty(result.PageNumbers);
        Assert.NotNull(result.ExtractedData);
        Assert.Empty(result.ExtractedData);
        Assert.Equal(string.Empty, result.OutputBlobName);
    }

    [Fact]
    public void DocumentResult_CanSetAllProperties()
    {
        // Arrange & Act
        var result = new DocumentResult
        {
            DocumentNumber = 1,
            PageCount = 5,
            PageNumbers = new List<int> { 1, 2, 3, 4, 5 },
            ExtractedData = new Dictionary<string, object> { { "Content", "Test" } },
            OutputBlobName = "test_doc_1.pdf"
        };
        
        // Assert
        Assert.Equal(1, result.DocumentNumber);
        Assert.Equal(5, result.PageCount);
        Assert.Equal(5, result.PageNumbers.Count);
        Assert.Single(result.ExtractedData);
        Assert.Equal("test_doc_1.pdf", result.OutputBlobName);
    }
}
