using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class SubjectsEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Id = "11111111-1111-1111-1111-111111111111";
    private readonly TestWebApplicationFactory _factory;
    public SubjectsEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/subjects")]
    [InlineData("POST", "/api/subjects")]
    [InlineData("PUT", "/api/subjects/" + Id)]
    [InlineData("DELETE", "/api/subjects/" + Id)]
    public async Task Endpoint_Unauthenticated_Returns401(string method, string url)
    {
        var response = await _factory.CreateClient()
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_Authenticated_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/subjects");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
