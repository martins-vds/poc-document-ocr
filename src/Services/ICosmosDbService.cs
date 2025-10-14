using DocumentOcrProcessor.Models;

namespace DocumentOcrProcessor.Services;

public interface ICosmosDbService
{
    Task<DocumentOcrEntity> CreateDocumentAsync(DocumentOcrEntity entity);
}
