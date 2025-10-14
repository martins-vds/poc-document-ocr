namespace DocumentOcrProcessor.Models;

public class QueueMessage
{
    public string BlobName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public bool UseManualDetection { get; set; }
    public string IdentifierFieldName { get; set; } = "identifier";
}
