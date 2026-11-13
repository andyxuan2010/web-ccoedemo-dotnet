using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebCcoeDemo.Web.Tests;

public sealed class ApplicationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApplicationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSetting("environment", "Development"));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/healthz")]
    public async Task ReadinessEndpoints_ReturnJsonWithoutRedirect(string path)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("{\"status\":\"ready\"}", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task HeadReadinessEndpoint_HasNoBody()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, "/health");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task LandingPage_ReturnsSecurityHeaders()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Theory]
    [InlineData("/profile/msal", "/login/msal")]
    [InlineData("/profile/easyauth", "/login/easyauth")]
    public async Task ProfileWithoutIdentity_RedirectsToMatchingLogin(string path, string expectedLocation)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CallbackWithoutSessionState_ReturnsSafeErrorPage()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/auth/callback?state=untrusted&code=ignored");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("invalid_state", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ignored", content, StringComparison.Ordinal);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });
}
