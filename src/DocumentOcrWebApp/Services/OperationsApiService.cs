using System.Text;
using System.Text.Json;
using DocumentOcrWebApp.Models;

namespace DocumentOcrWebApp.Services;

public class OperationsApiService : IOperationsApiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OperationsApiService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionKey;

    public OperationsApiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OperationsApiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _baseUrl = _configuration["OperationsApi:BaseUrl"] ?? throw new InvalidOperationException("OperationsApi:BaseUrl is not configured");
        _functionKey = _configuration["OperationsApi:FunctionKey"] ?? "";
    }

    public async Task<List<OperationDto>> GetOperationsAsync(string? status = null, int? maxItems = null)
    {
        try
        {
            var url = $"{_baseUrl}/api/operations";
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(status))
                queryParams.Add($"status={Uri.EscapeDataString(status)}");
            
            if (maxItems.HasValue)
                queryParams.Add($"maxItems={maxItems.Value}");
            
            if (!string.IsNullOrEmpty(_functionKey))
                queryParams.Add($"code={Uri.EscapeDataString(_functionKey)}");

            if (queryParams.Any())
                url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OperationsListResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Operations ?? new List<OperationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching operations from API");
            throw;
        }
    }

    public async Task<OperationDto?> GetOperationAsync(string operationId)
    {
        try
        {
            var url = $"{_baseUrl}/api/operations/{operationId}";
            if (!string.IsNullOrEmpty(_functionKey))
                url += $"?code={Uri.EscapeDataString(_functionKey)}";

            var response = await _httpClient.GetAsync(url);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OperationDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching operation {OperationId} from API", operationId);
            throw;
        }
    }

    public async Task<OperationDto> CancelOperationAsync(string operationId)
    {
        try
        {
            var url = $"{_baseUrl}/api/operations/{operationId}/cancel";
            if (!string.IsNullOrEmpty(_functionKey))
                url += $"?code={Uri.EscapeDataString(_functionKey)}";

            var response = await _httpClient.PostAsync(url, null);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OperationDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize cancel response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling operation {OperationId}", operationId);
            throw;
        }
    }

    public async Task<OperationDto> RetryOperationAsync(string operationId)
    {
        try
        {
            var url = $"{_baseUrl}/api/operations/{operationId}/retry";
            if (!string.IsNullOrEmpty(_functionKey))
                url += $"?code={Uri.EscapeDataString(_functionKey)}";

            var response = await _httpClient.PostAsync(url, null);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OperationDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize retry response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying operation {OperationId}", operationId);
            throw;
        }
    }
}
