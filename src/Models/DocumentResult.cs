namespace DocumentOcrProcessor.Models;

public class DocumentResult
{
    public int DocumentNumber { get; set; }
    public int PageCount { get; set; }
    public List<int> PageNumbers { get; set; } = new();
    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public string OutputBlobName { get; set; } = string.Empty;
}
