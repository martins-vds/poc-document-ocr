namespace DocumentOcrProcessor.Models;

public class PageOcrResult
{
    public int PageNumber { get; set; }
    public Stream ImageStream { get; set; } = Stream.Null;
    public Dictionary<string, object> ExtractedData { get; set; } = new();
}
