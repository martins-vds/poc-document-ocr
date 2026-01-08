using DocumentOcr.Processor.Models;
using Xunit;

namespace DocumentOcr.Tests.Models;

public class ProcessingResultTests
{
    [Fact]
    public void ProcessingResult_DefaultValues_AreInitialized()
    {
        // Arrange & Act
        var result = new ProcessingResult();
        
        // Assert
        Assert.Equal(string.Empty, result.OriginalFileName);
        Assert.Equal(0, result.TotalDocuments);
        Assert.NotNull(result.Documents);
        Assert.Empty(result.Documents);
        Assert.NotEqual(DateTime.MinValue, result.ProcessedAt);
    }

    [Fact]
    public void ProcessingResult_ProcessedAt_IsSetToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);
        
        // Act
        var result = new ProcessingResult();
        var afterCreation = DateTime.UtcNow.AddSeconds(1);
        
        // Assert
        Assert.True(result.ProcessedAt >= beforeCreation);
        Assert.True(result.ProcessedAt <= afterCreation);
    }

    [Fact]
    public void ProcessingResult_CanAddDocuments()
    {
        // Arrange
        var result = new ProcessingResult
        {
            OriginalFileName = "test.pdf",
            TotalDocuments = 2
        };
        
        // Act
        result.Documents.Add(new DocumentResult { DocumentNumber = 1 });
        result.Documents.Add(new DocumentResult { DocumentNumber = 2 });
        
        // Assert
        Assert.Equal(2, result.Documents.Count);
        Assert.Equal("test.pdf", result.OriginalFileName);
        Assert.Equal(2, result.TotalDocuments);
    }
}
