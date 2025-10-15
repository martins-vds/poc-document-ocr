namespace DocumentOcrProcessor.Models;

public class AggregatedDocument
{
    public string Identifier { get; set; } = string.Empty;
    public List<PageOcrResult> Pages { get; set; } = new();
}
