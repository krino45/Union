using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class SuggestEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public SuggestEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/suggest?text=abc");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Authenticated_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        // Short text short-circuits to an empty result without calling the external API.
        var response = await client.GetAsync("/api/suggest?text=ab");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
