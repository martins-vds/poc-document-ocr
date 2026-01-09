using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentOcr.Processor.Services;

/// <summary>
/// Service for sending messages to Azure Storage Queues.
/// </summary>
public class QueueService : IQueueService
{
    private const string QueueName = "pdf-processing-queue";
    private readonly ILogger<QueueService> _logger;
    private readonly QueueClient _queueClient;

    public QueueService(IConfiguration configuration, ILogger<QueueService> logger)
    {
        _logger = logger;
        _queueClient = CreateQueueClient(configuration);
    }

    private static QueueClient CreateQueueClient(IConfiguration configuration)
    {
        var queueServiceUriString = configuration["AzureWebJobsStorage:queueServiceUri"];

        if (!string.IsNullOrEmpty(queueServiceUriString))
        {
            // Use Managed Identity with queue service URI
            if (queueServiceUriString.EndsWith('/'))
            {
                queueServiceUriString = $"{queueServiceUriString}{QueueName}";
            }
            else
            {
                queueServiceUriString = $"{queueServiceUriString}/{QueueName}";
            }

            if (!Uri.TryCreate(queueServiceUriString, new UriCreationOptions(), out var queueServiceUri))
            {
                throw new InvalidOperationException("Invalid queue service URI");
            }

            return new QueueClient(queueServiceUri, new DefaultAzureCredential(), new() { MessageEncoding = QueueMessageEncoding.Base64 });
        }

        // Fall back to connection string
        var connectionString = configuration["AzureWebJobsStorage"];

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Queue service URI or connection string must be provided in configuration");
        }

        return new QueueClient(connectionString, QueueName, new() { MessageEncoding = QueueMessageEncoding.Base64 });
    }

    /// <inheritdoc/>
    public async Task SendMessageAsync(string message)
    {
        await _queueClient.CreateIfNotExistsAsync();
        await _queueClient.SendMessageAsync(message);
        _logger.LogDebug("Message sent to queue {QueueName}", QueueName);
    }
}
