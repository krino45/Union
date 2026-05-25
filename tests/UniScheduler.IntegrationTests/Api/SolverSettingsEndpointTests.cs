using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class SolverSettingsEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public SolverSettingsEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/solver-settings")]
    [InlineData("PUT", "/api/solver-settings")]
    public async Task Endpoint_Unauthenticated_Returns401(string method, string url)
    {
        var response = await _factory.CreateClient()
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Authenticated_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/solver-settings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
