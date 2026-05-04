using Azure.Identity;
using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Services;
using DocumentOcr.Processor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
builder.Services.AddSingleton<IQueueService, QueueService>();

// Register Cosmos DB client as singleton
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var endpoint = configuration["CosmosDb:Endpoint"];

    if (string.IsNullOrEmpty(endpoint))
    {
        throw new InvalidOperationException("Cosmos DB configuration is missing. Please configure CosmosDb:Endpoint.");
    }

    // Local-development fallback: when CosmosDb:Key is set (e.g. the Cosmos
    // emulator's well-known key), authenticate with the shared key and
    // accept the emulator's self-signed cert. Production stays on AAD.
    var key = configuration["CosmosDb:Key"];
    if (!string.IsNullOrEmpty(key))
    {
        var options = new Microsoft.Azure.Cosmos.CosmosClientOptions
        {
            ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway,
            HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            }),
        };
        return new Microsoft.Azure.Cosmos.CosmosClient(endpoint, key, options);
    }

    return new Microsoft.Azure.Cosmos.CosmosClient(endpoint, new DefaultAzureCredential());
});

builder.Services.AddScoped<ICosmosDbService, CosmosDbService>();
builder.Services.AddScoped<IOperationService, OperationService>();
builder.Services.AddScoped<IDocumentSchemaMapperService, DocumentSchemaMapperService>();

builder.Build().Run();
