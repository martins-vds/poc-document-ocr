using DocumentOcr.Processor.Models;
using Xunit;

namespace DocumentOcr.Tests.Models;

public class OperationTests
{
    [Fact]
    public void Operation_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var operation = new Operation();
        
        // Assert
        Assert.NotEqual(string.Empty, operation.Id); // Should have a GUID
        Assert.Equal(OperationStatus.NotStarted, operation.Status);
        Assert.Equal(string.Empty, operation.BlobName);
        Assert.Equal(string.Empty, operation.ContainerName);
        Assert.Equal("identifier", operation.IdentifierFieldName);
        Assert.Equal(0, operation.ProcessedDocuments);
        Assert.Equal(0, operation.TotalDocuments);
        Assert.False(operation.CancelRequested);
        Assert.Null(operation.Error);
        Assert.Null(operation.StartedAt);
        Assert.Null(operation.CompletedAt);
        Assert.Null(operation.ResultBlobName);
        Assert.Null(operation.ResourceUrl);
    }

    [Fact]
    public void Operation_CanSetProperties()
    {
        // Arrange
        var operationId = Guid.NewGuid().ToString();
        var createdAt = DateTime.UtcNow;
        var startedAt = DateTime.UtcNow.AddMinutes(1);
        var completedAt = DateTime.UtcNow.AddMinutes(5);
        
        // Act
        var operation = new Operation
        {
            Id = operationId,
            Status = OperationStatus.Running,
            BlobName = "test.pdf",
            ContainerName = "uploaded-pdfs",
            IdentifierFieldName = "customId",
            CreatedAt = createdAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ProcessedDocuments = 5,
            TotalDocuments = 10,
            ResultBlobName = "result.json",
            CancelRequested = true,
            Error = "Test error",
            ResourceUrl = "https://api.example.com/operations/123"
        };
        
        // Assert
        Assert.Equal(operationId, operation.Id);
        Assert.Equal(OperationStatus.Running, operation.Status);
        Assert.Equal("test.pdf", operation.BlobName);
        Assert.Equal("uploaded-pdfs", operation.ContainerName);
        Assert.Equal("customId", operation.IdentifierFieldName);
        Assert.Equal(createdAt, operation.CreatedAt);
        Assert.Equal(startedAt, operation.StartedAt);
        Assert.Equal(completedAt, operation.CompletedAt);
        Assert.Equal(5, operation.ProcessedDocuments);
        Assert.Equal(10, operation.TotalDocuments);
        Assert.Equal("result.json", operation.ResultBlobName);
        Assert.True(operation.CancelRequested);
        Assert.Equal("Test error", operation.Error);
        Assert.Equal("https://api.example.com/operations/123", operation.ResourceUrl);
    }

    [Theory]
    [InlineData(OperationStatus.NotStarted)]
    [InlineData(OperationStatus.Running)]
    [InlineData(OperationStatus.Succeeded)]
    [InlineData(OperationStatus.Failed)]
    [InlineData(OperationStatus.Cancelled)]
    public void Operation_StatusEnum_AllValues_CanBeSet(OperationStatus status)
    {
        // Arrange & Act
        var operation = new Operation { Status = status };
        
        // Assert
        Assert.Equal(status, operation.Status);
    }
}
