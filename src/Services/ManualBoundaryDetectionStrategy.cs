using Microsoft.Extensions.Logging;

namespace DocumentOcrProcessor.Services;

public class ManualBoundaryDetectionStrategy : IDocumentBoundaryDetectionStrategy
{
    private readonly ILogger<ManualBoundaryDetectionStrategy> _logger;

    public ManualBoundaryDetectionStrategy(ILogger<ManualBoundaryDetectionStrategy> logger)
    {
        _logger = logger;
    }

    public Task<List<int>> DetectDocumentBoundariesAsync(Stream pdfStream, int totalPages)
    {
        _logger.LogInformation("Using manual document boundary detection - treating entire PDF as single document");
        _logger.LogInformation("User should implement custom logic to detect document boundaries as needed");
        
        // Default implementation: treat entire PDF as single document
        // Users can extend this class or implement their own detection logic
        return Task.FromResult(new List<int> { 1 });
    }
}
