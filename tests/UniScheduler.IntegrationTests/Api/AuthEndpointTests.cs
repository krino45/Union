using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace UniScheduler.IntegrationTests.Api;

public class AuthEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "admin123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Role.Should().Be("SuperAdmin");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "WRONG" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "nobody", Password = "any" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Returns401ResponseWithProblemJson()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "bad" });

        response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Unauthorized");
    }

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/auth/renew")]
    [InlineData("POST", "/api/auth/accept-invitation")]
    public async Task ProtectedEndpoint_Unauthenticated_Returns401(string method, string url)
    {
        var response = await _factory.CreateClient()
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_IsAnonymous_Returns204()
    {
        var response = await _factory.CreateClient().PostAsync("/api/auth/logout", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetInvitationInfo_UnknownToken_IsReachableNotUnauthorized()
    {
        // [AllowAnonymous]: the route should resolve and run the handler, never 401.
        var response = await _factory.CreateClient().GetAsync($"/api/auth/invitation/{Guid.NewGuid():N}");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    private sealed record LoginBody(string Token, string Role, Guid UserId, Guid? TeacherId);
}
