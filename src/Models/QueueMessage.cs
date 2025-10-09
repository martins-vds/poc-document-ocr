namespace DocumentOcrProcessor.Models;

public class QueueMessage
{
    public string BlobName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public List<int>? ManualBoundaries { get; set; }
    public bool UseManualDetection { get; set; }
}
