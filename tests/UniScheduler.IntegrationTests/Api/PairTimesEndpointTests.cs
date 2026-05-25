using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class PairTimesEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public PairTimesEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    // This endpoint is intentionally anonymous (no [Authorize]).
    [Fact]
    public async Task GetAll_Unauthenticated_Returns200()
    {
        var response = await _factory.CreateClient().GetAsync("/api/pair-times");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_Authenticated_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/pair-times");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
