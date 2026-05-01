using DocumentOcr.Common.Models;
using DocumentOcr.Processor.Models;

namespace DocumentOcr.Processor.Services;

/// <summary>
/// Maps a single <see cref="AggregatedDocument"/> (per-page OCR output that
/// already shares an identifier) to a consolidated <see cref="DocumentOcrEntity"/>
/// per data-model.md and contracts/IDocumentSchemaMapperService.md.
/// </summary>
public interface IDocumentSchemaMapperService
{
    /// <summary>
    /// Build a consolidated document. Pages are in source-PDF page order.
    /// Identifier may be a synthetic <c>unknown-{blob}-{firstPage}</c> fallback.
    /// </summary>
    /// <exception cref="ArgumentException">When the aggregated document has zero pages.</exception>
    DocumentOcrEntity Map(
        AggregatedDocument aggregatedDocument,
        int documentNumber,
        string originalFileName,
        string pdfBlobUrl,
        string outputBlobName);
}
