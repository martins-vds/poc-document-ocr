using DocumentOcr.WebApp.Models;

namespace DocumentOcr.WebApp.Services;

public interface IOperationsApiService
{
    Task<List<OperationDto>> GetOperationsAsync(string? status = null, int? maxItems = null);
    Task<OperationDto?> GetOperationAsync(string operationId);
    Task<OperationDto> StartOperationAsync(string blobName, string containerName, string? identifierFieldName = null);
    Task<OperationDto> CancelOperationAsync(string operationId);
    Task<OperationDto> RetryOperationAsync(string operationId);
}
