using DocumentOcrProcessor.Models;

namespace DocumentOcrProcessor.Services;

public interface IDocumentAggregatorService
{
    List<AggregatedDocument> AggregatePagesByIdentifier(List<PageOcrResult> pageResults, string identifierFieldName);
}
