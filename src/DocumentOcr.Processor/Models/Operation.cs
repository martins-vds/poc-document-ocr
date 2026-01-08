using Newtonsoft.Json;

namespace DocumentOcr.Processor.Models;

public class Operation
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("status")]
    public OperationStatus Status { get; set; } = OperationStatus.NotStarted;

    [JsonProperty("blobName")]
    public string BlobName { get; set; } = string.Empty;

    [JsonProperty("containerName")]
    public string ContainerName { get; set; } = string.Empty;

    [JsonProperty("identifierFieldName")]
    public string IdentifierFieldName { get; set; } = "identifier";

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonProperty("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("processedDocuments")]
    public int ProcessedDocuments { get; set; }

    [JsonProperty("totalDocuments")]
    public int TotalDocuments { get; set; }

    [JsonProperty("resultBlobName")]
    public string? ResultBlobName { get; set; }

    [JsonProperty("cancelRequested")]
    public bool CancelRequested { get; set; }

    [JsonProperty("resourceUrl")]
    public string? ResourceUrl { get; set; }

    public Operation()
    {
        CreatedAt = DateTime.UtcNow;
    }
}

public enum OperationStatus
{
    NotStarted,
    Running,
    Succeeded,
    Failed,
    Cancelled
}
