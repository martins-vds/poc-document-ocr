using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentOcr.IntegrationTests.Fixtures;

/// <summary>
/// Hosts <c>DocumentOcr.WebApp</c>'s <see cref="DocumentOcr.WebApp.Program"/>
/// in-process via <see cref="WebApplicationFactory{TEntryPoint}"/>. The
/// production Cosmos client and Microsoft.Identity.Web auth pipeline are
/// replaced with test doubles so controllers can be exercised without an
/// Azure tenant or live Cosmos endpoint.
///
/// Tests build a per-scenario factory via <see cref="ForScenario"/>
/// because <see cref="WebApplicationFactory{TEntryPoint}"/> caches the host
/// after the first <c>CreateClient</c> call.
/// </summary>
public sealed class WebAppFactory : WebApplicationFactory<DocumentOcr.WebApp.Program>
{
    public const string TestAuthScheme = "TestScheme";

    public WebApplicationFactory<DocumentOcr.WebApp.Program> ForScenario(
        string? authenticatedUpn = null,
        Action<IServiceCollection>? configureServices = null)
    {
        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                ApplyAuth(services, authenticatedUpn);
                configureServices?.Invoke(services);
            });
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
                ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000001",
                ["AzureAd:Domain"] = "test.local",
                ["CosmosDb:Endpoint"] = "https://localhost:8081/",
                ["CosmosDb:DatabaseName"] = "TestDb",
                ["CosmosDb:ContainerName"] = "TestContainer",
                ["Storage:AccountName"] = "devstoreaccount1",
                ["AzureWebJobsStorage"] = AzuriteFixture.ConnectionString,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace CosmosClient with a Moq-backed default so host startup
            // does not require a live Cosmos endpoint.
            foreach (var d in services.Where(d => d.ServiceType == typeof(CosmosClient)).ToList())
            {
                services.Remove(d);
            }
            services.AddSingleton(new Mock<CosmosClient>().Object);
        });
    }

    private static void ApplyAuth(IServiceCollection services, string? authenticatedUpn)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthScheme;
                options.DefaultChallengeScheme = TestAuthScheme;
                options.DefaultScheme = TestAuthScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthScheme, _ => { });

        services.Configure<TestAuthHandlerOptions>(o =>
        {
            o.Authenticated = authenticatedUpn is not null;
            o.Upn = authenticatedUpn ?? "anonymous@example.com";
        });
    }
}

internal sealed class TestAuthHandlerOptions
{
    public bool Authenticated { get; set; }
    public string Upn { get; set; } = "test-user@example.com";
}

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TestAuthHandlerOptions _testOptions;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<TestAuthHandlerOptions> testOptions)
        : base(options, logger, encoder)
    {
        _testOptions = testOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_testOptions.Authenticated)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, _testOptions.Upn),
            new Claim("preferred_username", _testOptions.Upn),
        }, WebAppFactory.TestAuthScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, WebAppFactory.TestAuthScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
