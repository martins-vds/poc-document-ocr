namespace DocumentOcrWebApp.Models;

public class OperationDto
{
    public string OperationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProcessedDocuments { get; set; }
    public int TotalDocuments { get; set; }
    public string? ResultBlobName { get; set; }
    public string? Error { get; set; }
    public bool CancelRequested { get; set; }
}

public class OperationsListResponse
{
    public List<OperationDto> Operations { get; set; } = new();
    public int Count { get; set; }
}

public class StartOperationResponse
{
    public string OperationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusQueryGetUri { get; set; } = string.Empty;
}
