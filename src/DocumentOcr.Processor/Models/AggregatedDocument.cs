using DocumentOcr.Common.Models;

namespace DocumentOcr.Processor.Models;

public class AggregatedDocument
{
    public string Identifier { get; set; } = string.Empty;
    public List<PageOcrResult> Pages { get; set; } = new();

    /// <summary>
    /// One entry per page in this consolidated document indicating whether
    /// the identifier was extracted by OCR or forward-filled (FR-020 / T032).
    /// </summary>
    public List<PageProvenanceEntry> PageProvenance { get; set; } = new();
}
