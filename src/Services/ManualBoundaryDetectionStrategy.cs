using Microsoft.Extensions.Logging;

namespace DocumentOcrProcessor.Services;

public class ManualBoundaryDetectionStrategy : IDocumentBoundaryDetectionStrategy
{
    private readonly ILogger<ManualBoundaryDetectionStrategy> _logger;

    public ManualBoundaryDetectionStrategy(ILogger<ManualBoundaryDetectionStrategy> logger)
    {
        _logger = logger;
    }

    public Task<List<int>> DetectDocumentBoundariesAsync(Stream pdfStream, int totalPages, List<int>? manualBoundaries = null)
    {
        _logger.LogInformation("Using manual document boundary detection");

        if (manualBoundaries == null || manualBoundaries.Count == 0)
        {
            _logger.LogWarning("No manual boundaries provided, treating entire PDF as single document");
            return Task.FromResult(new List<int> { 1 });
        }

        var boundaries = ValidateAndNormalizeBoundaries(manualBoundaries, totalPages);
        _logger.LogInformation("Using manual boundaries: {Boundaries}", string.Join(", ", boundaries));
        
        return Task.FromResult(boundaries);
    }

    private List<int> ValidateAndNormalizeBoundaries(List<int> boundaries, int totalPages)
    {
        var validBoundaries = new List<int>();

        foreach (var boundary in boundaries)
        {
            if (boundary > 0 && boundary <= totalPages)
            {
                validBoundaries.Add(boundary);
            }
            else
            {
                _logger.LogWarning("Invalid boundary page {Page} for PDF with {TotalPages} pages, skipping", 
                    boundary, totalPages);
            }
        }

        if (validBoundaries.Count == 0 || validBoundaries[0] != 1)
        {
            validBoundaries.Insert(0, 1);
        }

        validBoundaries.Sort();
        return validBoundaries.Distinct().ToList();
    }
}
