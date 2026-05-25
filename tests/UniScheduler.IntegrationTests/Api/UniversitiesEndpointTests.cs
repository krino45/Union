using System.Net;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class UniversitiesEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Id = "11111111-1111-1111-1111-111111111111";
    private readonly TestWebApplicationFactory _factory;
    public UniversitiesEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/universities")]
    [InlineData("POST", "/api/universities")]
    [InlineData("PUT", "/api/universities/" + Id)]
    [InlineData("DELETE", "/api/universities/" + Id)]
    [InlineData("GET", "/api/universities/" + Id + "/users")]
    [InlineData("POST", "/api/universities/" + Id + "/users")]
    [InlineData("DELETE", "/api/universities/" + Id + "/users/" + Id)]
    [InlineData("POST", "/api/universities/" + Id + "/grant-self")]
    [InlineData("GET", "/api/universities/" + Id + "/invitations")]
    [InlineData("POST", "/api/universities/" + Id + "/invitations")]
    [InlineData("DELETE", "/api/universities/invitations/" + Id)]
    public async Task Endpoint_Unauthenticated_Returns401(string method, string url)
    {
        var response = await _factory.CreateClient()
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_AuthenticatedAsSuperAdmin_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/universities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
