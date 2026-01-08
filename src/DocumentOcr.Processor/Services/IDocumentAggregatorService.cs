using DocumentOcr.Processor.Models;

namespace DocumentOcr.Processor.Services;

public interface IDocumentAggregatorService
{
    List<AggregatedDocument> AggregatePagesByIdentifier(List<PageOcrResult> pageResults, string identifierFieldName);
}
