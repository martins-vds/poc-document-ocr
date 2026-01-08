using Azure.Identity;
using DocumentOcr.Processor.Services;
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

builder.Services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IPdfToImageService, PdfToImageService>();
builder.Services.AddScoped<IDocumentAggregatorService, DocumentAggregatorService>();
builder.Services.AddScoped<IImageToPdfService, ImageToPdfService>();

// Register Cosmos DB client as singleton
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var endpoint = configuration["CosmosDb:Endpoint"];

    if (string.IsNullOrEmpty(endpoint))
    {
        throw new InvalidOperationException("Cosmos DB configuration is missing. Please configure CosmosDb:Endpoint.");
    }

    return new Microsoft.Azure.Cosmos.CosmosClient(endpoint, new DefaultAzureCredential());
});

builder.Services.AddScoped<ICosmosDbService, CosmosDbService>();
builder.Services.AddScoped<IOperationService, OperationService>();

builder.Build().Run();
