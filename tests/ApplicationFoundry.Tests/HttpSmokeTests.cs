using Microsoft.AspNetCore.Mvc.Testing;

namespace ApplicationFoundry.Tests;

public sealed class HttpSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public HttpSmokeTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task Health_endpoint_is_healthy()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
