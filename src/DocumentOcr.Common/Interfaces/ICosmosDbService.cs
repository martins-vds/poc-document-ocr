using DocumentOcr.Common.Models;

namespace DocumentOcr.Common.Interfaces;

public interface ICosmosDbService
{
    Task<DocumentOcrEntity> CreateDocumentAsync(DocumentOcrEntity entity);
    Task<DocumentOcrEntity> UpdateDocumentAsync(DocumentOcrEntity entity);
    Task<DocumentOcrEntity?> GetDocumentByIdAsync(string id, string partitionKey);
    Task<List<DocumentOcrEntity>> GetDocumentsAsync(string? reviewStatus = null, int? maxItems = null);

    /// <summary>
    /// T018 — ETag-conditional replace. Uses the entity's <c>ETag</c> as
    /// <c>IfMatchEtag</c>. Throws <c>CosmosException</c> with status 412
    /// when another writer has updated the document.
    /// </summary>
    Task<DocumentOcrEntity> ReplaceWithETagAsync(DocumentOcrEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// T018a — Single-partition point query keyed on <c>identifier</c>
    /// (which is the partition key). Returns the existing record for the
    /// given <c>fileTkNumber</c> or <c>null</c>. Backs the FR-019
    /// duplicate-skip behavior.
    /// </summary>
    Task<DocumentOcrEntity?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
}
