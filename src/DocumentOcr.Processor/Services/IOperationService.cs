using DocumentOcr.Processor.Models;

namespace DocumentOcr.Processor.Services;

public interface IOperationService
{
    Task<Operation> CreateOperationAsync(string blobName, string containerName, string identifierFieldName = "identifier");
    Task<Operation?> GetOperationAsync(string operationId);
    Task<Operation> UpdateOperationAsync(Operation operation);
    Task<List<Operation>> GetOperationsAsync(OperationStatus? status = null, int? maxItems = null);
    Task<Operation> CancelOperationAsync(string operationId);
}
