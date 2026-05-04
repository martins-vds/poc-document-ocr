using System.Net;
using DocumentOcr.IntegrationTests.Fixtures;

namespace DocumentOcr.IntegrationTests.WebApp.Auth;

/// <summary>
/// Verifies the WebApp's auth pipeline at the boundary: protected endpoints
/// must challenge anonymous callers, and the test auth handler must satisfy
/// the same endpoints when an authenticated principal is presented.
/// </summary>
public sealed class AuthPipelineTests : IClassFixture<WebAppFactory>
{
    private readonly WebAppFactory _factory;

    public AuthPipelineTests(WebAppFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/pdf/doc/tk")]
    [InlineData("/api/review/checkout")]
    public async Task ProtectedEndpoints_RejectAnonymous(string path)
    {
        using var factory = _factory.ForScenario();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = path.StartsWith("/api/review")
            ? await client.PostAsync(path, content: null)
            : await client.GetAsync(path);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.Redirect
                or HttpStatusCode.Found,
            $"{path} should challenge anonymous callers; got {response.StatusCode}.");
    }

    [Fact]
    public async Task AuthenticatedPrincipal_PassesAuthorization()
    {
        // Hit a known authenticated endpoint. The PDF endpoint will hit
        // Cosmos which is mocked at factory level — a 404 here means we
        // cleared the [Authorize] gate.
        using var factory = _factory.ForScenario(authenticatedUpn: "reviewer@example.com");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/pdf/missing/x");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
    }
}
