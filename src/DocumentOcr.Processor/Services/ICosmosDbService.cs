using DocumentOcr.Processor.Models;

namespace DocumentOcr.Processor.Services;

public interface ICosmosDbService
{
    Task<DocumentOcrEntity> CreateDocumentAsync(DocumentOcrEntity entity);
    Task<DocumentOcrEntity> UpdateDocumentAsync(DocumentOcrEntity entity);
    Task<DocumentOcrEntity?> GetDocumentByIdAsync(string id, string partitionKey);
    Task<List<DocumentOcrEntity>> GetDocumentsAsync(string? reviewStatus = null, int? maxItems = null);
}
