using Azure.Identity;
using Azure.Storage.Blobs;
using DocumentOcrProcessor.Services;
using DocumentOcrWebApp.Components;
using DocumentOcrWebApp.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Azure.Cosmos;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

namespace DocumentOcrWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add authentication
            builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

            builder.Services.AddAuthorization();

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddControllersWithViews()
                .AddMicrosoftIdentityUI();

            // Register Cosmos DB client
            builder.Services.AddSingleton<CosmosClient>(sp =>
            {
                var endpoint = builder.Configuration["CosmosDb:Endpoint"] ?? throw new InvalidOperationException("CosmosDb:Endpoint is not configured");
                var cosmosClient = new CosmosClient(endpoint, new DefaultAzureCredential());
                return cosmosClient;
            });

            // Register Cosmos DB service
            builder.Services.AddScoped<ICosmosDbService, CosmosDbService>();

            // Register Blob Storage service
            builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

            // Register HTTP client and Operations API service
            builder.Services.AddHttpClient<IOperationsApiService, OperationsApiService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.MapControllers();

            app.Run();
        }
    }
}
