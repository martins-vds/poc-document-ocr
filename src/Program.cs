using DocumentOcrProcessor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register document boundary detection strategy based on configuration
builder.Services.AddScoped<IDocumentBoundaryDetectionStrategy>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var useManualDetection = config.GetValue<bool>("DocumentBoundaryDetection:UseManual", false);
    
    if (useManualDetection)
    {
        var logger = sp.GetRequiredService<ILogger<ManualBoundaryDetectionStrategy>>();
        return new ManualBoundaryDetectionStrategy(logger);
    }
    else
    {
        var logger = sp.GetRequiredService<ILogger<AiBoundaryDetectionStrategy>>();
        return new AiBoundaryDetectionStrategy(logger, config);
    }
});

builder.Services.AddScoped<IPdfSplitterService, PdfSplitterService>();
builder.Services.AddScoped<IAiFoundryService, AiFoundryService>();
builder.Services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();

builder.Build().Run();
