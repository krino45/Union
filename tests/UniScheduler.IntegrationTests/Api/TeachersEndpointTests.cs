using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class TeachersEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public TeachersEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/teachers")]
    [InlineData("POST", "/api/teachers")]
    [InlineData("PUT", "/api/teachers/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/teachers/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/teachers/11111111-1111-1111-1111-111111111111/subjects")]
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
        var response = await client.GetAsync("/api/teachers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
