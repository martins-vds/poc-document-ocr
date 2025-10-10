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

builder.Services.AddScoped<IPdfSplitterService, PdfSplitterService>();
builder.Services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// Register document boundary detection strategies
builder.Services.AddScoped<AiBoundaryDetectionStrategy>();
builder.Services.AddScoped<ManualBoundaryDetectionStrategy>();

builder.Build().Run();
