namespace DocumentOcr.Processor.Services;

/// <summary>
/// Service for sending messages to Azure Storage Queues.
/// </summary>
public interface IQueueService
{
    /// <summary>
    /// Sends a message to the PDF processing queue.
    /// </summary>
    /// <param name="message">The message content to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMessageAsync(string message);
}
