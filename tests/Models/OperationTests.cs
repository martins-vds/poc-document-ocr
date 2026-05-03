using DocumentOcr.Common.Models;
using DocumentOcr.Processor.Models;
using Newtonsoft.Json;

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

    [Fact]
    public void Operation_DefaultPageSelection_IsNull()
    {
        Assert.Null(new Operation().PageSelection);
    }

    [Fact]
    public void Operation_RoundTrip_NullPageSelection_StaysNull()
    {
        var op = new Operation { BlobName = "a.pdf", ContainerName = "c", PageSelection = null };

        var json = JsonConvert.SerializeObject(op);
        var restored = JsonConvert.DeserializeObject<Operation>(json)!;

        Assert.Null(restored.PageSelection);
        // NullValueHandling.Ignore on Operation.PageSelection: legacy documents
        // (no "pageSelection" property at all) must still deserialize cleanly.
        Assert.DoesNotContain("\"pageSelection\"", json);
    }

    [Fact]
    public void Operation_RoundTrip_ExplicitPageSelection_PreservesExpressionAndPages()
    {
        Assert.True(PageSelection.TryParse("3-12, 15", maxPage: null, out var sel, out _));
        var op = new Operation
        {
            BlobName = "a.pdf",
            ContainerName = "c",
            PageSelection = sel,
        };

        var json = JsonConvert.SerializeObject(op);
        var restored = JsonConvert.DeserializeObject<Operation>(json)!;

        Assert.NotNull(restored.PageSelection);
        Assert.Equal("3-12, 15", restored.PageSelection!.Expression);
        Assert.Equal(11, restored.PageSelection.Pages.Count);
        Assert.Equal(new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 15 }, restored.PageSelection.Pages);
        Assert.False(restored.PageSelection.IsAllPages);
    }

    [Fact]
    public void Operation_LegacyJson_WithoutPageSelection_DeserializesWithNull()
    {
        // A document persisted before feature 002 lacks the property entirely.
        var legacy = "{\"id\":\"abc\",\"blobName\":\"a.pdf\",\"containerName\":\"c\"}";

        var restored = JsonConvert.DeserializeObject<Operation>(legacy)!;

        Assert.Null(restored.PageSelection);
    }
}
