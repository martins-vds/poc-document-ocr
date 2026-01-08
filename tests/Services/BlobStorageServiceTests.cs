using DocumentOcr.Processor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentOcr.Tests.Services;

public class BlobStorageServiceTests
{
    [Fact]
    public void Constructor_WithMissingConnectionString_ThrowsException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<BlobStorageService>>();
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["AzureWebJobsStorage"]).Returns((string?)null);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            new BlobStorageService(mockLogger.Object, mockConfiguration.Object));
    }

    [Fact]
    public void Constructor_WithEmptyConnectionString_ThrowsException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<BlobStorageService>>();
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["AzureWebJobsStorage"]).Returns(string.Empty);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            new BlobStorageService(mockLogger.Object, mockConfiguration.Object));
    }
}
