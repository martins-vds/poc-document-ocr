namespace DocumentOcr.Processor.Models;

public class QueueMessage
{
    public string BlobName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public bool UseManualDetection { get; set; }

    /// <summary>
    /// Per feature 002: the original print-dialog–style page-range expression
    /// (e.g. <c>"3-12, 15"</c>). <c>null</c> or empty means "all pages". The
    /// worker re-parses this against the actual page count of the downloaded PDF.
    /// </summary>
    public string? PageRange { get; set; }
}
