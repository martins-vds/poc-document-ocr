using DocumentOcr.Processor.Functions;
using DocumentOcr.Processor.Models;
using DocumentOcr.Processor.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace DocumentOcr.UnitTests.Services;

/// <summary>
/// T011 — verifies that <see cref="OperationsApi.StartOperation"/> wires the
/// optional <c>pageRange</c> field through to the persisted
/// <see cref="Operation.PageSelection"/> and the queued
/// <see cref="QueueMessage.PageRange"/>, and that malformed expressions are
/// rejected with <c>400</c> before any side-effect.
///
/// Tests target the extracted internal <c>ProcessStartRequestAsync</c> helper
/// because the HTTP handler depends on the abstract <c>HttpRequestData</c>
/// type that requires a full Functions worker host to construct.
/// </summary>
public class OperationsApiStartTests
{
    private static (OperationsApi api, Mock<IOperationService> ops, Mock<IQueueService> queue) Build()
    {
        var ops = new Mock<IOperationService>();
        ops.Setup(o => o.CreateOperationAsync(It.IsAny<string>(), It.IsAny<string>()))
           .ReturnsAsync((string blob, string container) => new Operation { BlobName = blob, ContainerName = container });
        ops.Setup(o => o.UpdateOperationAsync(It.IsAny<Operation>()))
           .ReturnsAsync((Operation o) => o);

        var queue = new Mock<IQueueService>();
        queue.Setup(q => q.SendMessageAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var api = new OperationsApi(Mock.Of<ILogger<OperationsApi>>(), ops.Object, queue.Object);
        return (api, ops, queue);
    }

    [Fact]
    public async Task ProcessStartRequestAsync_NoPageRange_PersistsNullAndQueuesNullPageRange()
    {
        var (api, ops, queue) = Build();
        var request = new StartOperationRequest { BlobName = "a.pdf", ContainerName = "uploaded-pdfs" };
        string? capturedQueueMessage = null;
        queue.Setup(q => q.SendMessageAsync(It.IsAny<string>()))
             .Callback<string>(m => capturedQueueMessage = m)
             .Returns(Task.CompletedTask);

        var (status, body, op) = await api.ProcessStartRequestAsync(request, baseUrl: "https://h");

        Assert.Equal(202, status);
        Assert.Null(body);
        Assert.NotNull(op);
        Assert.Null(op!.PageSelection);
        Assert.NotNull(capturedQueueMessage);

        using var doc = JsonDocument.Parse(capturedQueueMessage!);
        var msg = doc.RootElement.GetProperty("Message");
        // Either absent or explicitly null is fine for "all pages".
        if (msg.TryGetProperty("PageRange", out var pr))
        {
            Assert.Equal(JsonValueKind.Null, pr.ValueKind);
        }
    }

    [Fact]
    public async Task ProcessStartRequestAsync_ValidPageRange_PersistsSelectionAndForwardsExpression()
    {
        var (api, _, queue) = Build();
        var request = new StartOperationRequest
        {
            BlobName = "a.pdf",
            ContainerName = "uploaded-pdfs",
            PageRange = "3-12, 15",
        };
        string? capturedQueueMessage = null;
        queue.Setup(q => q.SendMessageAsync(It.IsAny<string>()))
             .Callback<string>(m => capturedQueueMessage = m)
             .Returns(Task.CompletedTask);

        var (status, body, op) = await api.ProcessStartRequestAsync(request, baseUrl: "https://h");

        Assert.Equal(202, status);
        Assert.Null(body);
        Assert.NotNull(op);
        Assert.NotNull(op!.PageSelection);
        Assert.Equal("3-12, 15", op.PageSelection!.Expression);
        Assert.Equal(11, op.PageSelection.Pages.Count);

        Assert.NotNull(capturedQueueMessage);
        using var doc = JsonDocument.Parse(capturedQueueMessage!);
        Assert.Equal("3-12, 15", doc.RootElement.GetProperty("Message").GetProperty("PageRange").GetString());
    }

    [Fact]
    public async Task ProcessStartRequestAsync_MalformedPageRange_Returns400_AndDoesNothing()
    {
        var (api, ops, queue) = Build();
        var request = new StartOperationRequest
        {
            BlobName = "a.pdf",
            ContainerName = "uploaded-pdfs",
            PageRange = "abc",
        };

        var (status, body, op) = await api.ProcessStartRequestAsync(request, baseUrl: "https://h");

        Assert.Equal(400, status);
        Assert.NotNull(body);
        Assert.Null(op);
        ops.Verify(o => o.CreateOperationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        queue.Verify(q => q.SendMessageAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessStartRequestAsync_MissingBlobName_Returns400()
    {
        var (api, ops, queue) = Build();
        var request = new StartOperationRequest { BlobName = "", ContainerName = "c" };

        var (status, body, op) = await api.ProcessStartRequestAsync(request, baseUrl: "https://h");

        Assert.Equal(400, status);
        Assert.NotNull(body);
        Assert.Null(op);
        ops.Verify(o => o.CreateOperationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        queue.Verify(q => q.SendMessageAsync(It.IsAny<string>()), Times.Never);
    }
}
