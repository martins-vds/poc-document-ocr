using DocumentOcrProcessor.Models;
using Xunit;

namespace DocumentOcrProcessor.Tests.Models;

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
    }

    [Fact]
    public void QueueMessage_CanSetProperties()
    {
        // Arrange & Act
        var message = new QueueMessage
        {
            BlobName = "test.pdf",
            ContainerName = "uploaded-pdfs"
        };
        
        // Assert
        Assert.Equal("test.pdf", message.BlobName);
        Assert.Equal("uploaded-pdfs", message.ContainerName);
    }
}
