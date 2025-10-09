using DocumentOcrProcessor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentOcrProcessor.Tests.Services;

public class AiFoundryServiceTests
{
    [Fact]
    public void ParseBoundaries_WithValidCommaSeparatedNumbers_ReturnsCorrectBoundaries()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("1,5,10", 15);
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new List<int> { 1, 5, 10 }, result);
    }

    [Fact]
    public void ParseBoundaries_WithSpaceSeparatedNumbers_ReturnsCorrectBoundaries()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("1 5 10", 15);
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new List<int> { 1, 5, 10 }, result);
    }

    [Fact]
    public void ParseBoundaries_WithNewlineSeparatedNumbers_ReturnsCorrectBoundaries()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("1\n5\n10", 15);
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new List<int> { 1, 5, 10 }, result);
    }

    [Fact]
    public void ParseBoundaries_WithDuplicates_RemovesDuplicates()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("1,5,5,10", 15);
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new List<int> { 1, 5, 10 }, result);
    }

    [Fact]
    public void ParseBoundaries_WithoutStartingOne_AddsOne()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("5,10", 15);
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new List<int> { 1, 5, 10 }, result);
    }

    [Fact]
    public void ParseBoundaries_WithNumbersOutOfRange_IgnoresInvalidNumbers()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("1,5,20,25", 15);
        
        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new List<int> { 1, 5 }, result);
    }

    [Fact]
    public void ParseBoundaries_WithNegativeNumbers_IgnoresNegativeNumbers()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("1,-5,10", 15);
        
        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new List<int> { 1, 10 }, result);
    }

    [Fact]
    public void ParseBoundaries_WithZero_IgnoresZero()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("0,5,10", 15);
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new List<int> { 1, 5, 10 }, result);
    }

    [Fact]
    public void ParseBoundaries_WithEmptyString_ReturnsListWithOne()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("", 15);
        
        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0]);
    }

    [Fact]
    public void ParseBoundaries_WithNonNumericText_IgnoresNonNumeric()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("1,abc,5,xyz,10", 15);
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new List<int> { 1, 5, 10 }, result);
    }

    [Fact]
    public void ParseBoundaries_WithUnsortedNumbers_ReturnsSortedList()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = CreateMockConfiguration();
        var service = new AiFoundryService(mockLogger.Object, mockConfiguration.Object);
        
        // Act
        var result = service.ParseBoundaries("10,5,1", 15);
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new List<int> { 1, 5, 10 }, result);
    }

    [Fact]
    public void Constructor_WithMissingEndpoint_ThrowsException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["AzureAiFoundry:Endpoint"]).Returns((string?)null);
        mockConfiguration.Setup(c => c["AzureAiFoundry:ApiKey"]).Returns("test-key");
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            new AiFoundryService(mockLogger.Object, mockConfiguration.Object));
    }

    [Fact]
    public void Constructor_WithMissingApiKey_ThrowsException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiFoundryService>>();
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["AzureAiFoundry:Endpoint"]).Returns("https://test.endpoint.com");
        mockConfiguration.Setup(c => c["AzureAiFoundry:ApiKey"]).Returns((string?)null);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            new AiFoundryService(mockLogger.Object, mockConfiguration.Object));
    }

    private Mock<IConfiguration> CreateMockConfiguration()
    {
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["AzureAiFoundry:Endpoint"]).Returns("https://test.endpoint.com");
        mockConfiguration.Setup(c => c["AzureAiFoundry:ApiKey"]).Returns("test-api-key");
        return mockConfiguration;
    }
}
