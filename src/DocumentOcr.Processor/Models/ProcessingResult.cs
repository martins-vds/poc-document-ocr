namespace DocumentOcr.Processor.Models;

public class ProcessingResult
{
    public string OriginalFileName { get; set; } = string.Empty;
    public int TotalDocuments { get; set; }
    public List<DocumentResult> Documents { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// FR-019 — identifiers skipped because a record with that
    /// <c>fileTkNumber</c> already exists in Cosmos DB.
    /// </summary>
    public List<string> SkippedDuplicateIdentifiers { get; set; } = new();
}
