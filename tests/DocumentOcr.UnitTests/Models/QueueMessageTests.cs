using DocumentOcr.Processor.Models;
using System.Text.Json;

namespace DocumentOcr.UnitTests.Models;

public class QueueMessageTests
{
    [Fact]
    public void QueueMessage_DefaultValues_AreEmpty()
    {
        // Arrange & Act
        var message = new QueueMessage();

        // Assert
        Assert.Equal(string.Empty, message.BlobName);
        Assert.Equal(string.Empty, message.ContainerName);
        Assert.Null(message.PageRange);
    }

    [Fact]
    public void QueueMessage_CanSetProperties()
    {
        // Arrange & Act
        var message = new QueueMessage
        {
            BlobName = "test.pdf",
            ContainerName = "uploaded-pdfs",
            PageRange = "3-12, 15",
        };

        // Assert
        Assert.Equal("test.pdf", message.BlobName);
        Assert.Equal("uploaded-pdfs", message.ContainerName);
        Assert.Equal("3-12, 15", message.PageRange);
    }

    [Fact]
    public void QueueMessage_LegacyJson_WithoutPageRange_DeserializesWithNull()
    {
        // A queue payload produced before feature 002 lacks the new field.
        var legacy = "{\"BlobName\":\"x.pdf\",\"ContainerName\":\"uploaded-pdfs\"}";

        var restored = JsonSerializer.Deserialize<QueueMessage>(legacy)!;

        Assert.Equal("x.pdf", restored.BlobName);
        Assert.Null(restored.PageRange);
    }
}
