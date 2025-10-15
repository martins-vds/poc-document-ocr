namespace DocumentOcrProcessor.Models;

public class ProcessingResult
{
    public string OriginalFileName { get; set; } = string.Empty;
    public int TotalDocuments { get; set; }
    public List<DocumentResult> Documents { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
