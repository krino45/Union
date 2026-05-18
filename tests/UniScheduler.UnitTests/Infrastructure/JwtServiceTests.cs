using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using UniScheduler.Domain.Entities;
using UniScheduler.Infrastructure.Auth;
using Xunit;

namespace UniScheduler.UnitTests.Infrastructure;

public class JwtServiceTests
{
    private const string Secret = "TestSecretKeyWith32+CharactersForHmac";
    private const string Issuer = "TestIssuer";
    private const string Audience = "TestAudience";

    private static JwtService CreateSut() => new(BuildSection());

    private static IConfigurationSection BuildSection()
    {
        var data = new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = Secret,
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["Jwt:ExpiryDays"] = "7"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build().GetSection("Jwt");
    }

    private static AppUser MakeUser(string role = "Admin", Guid? teacherId = null) => new()
    {
        Username = "testuser",
        PasswordHash = "",
        Role = role,
        TeacherId = teacherId
    };

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        CreateSut().GenerateToken(MakeUser()).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_ProducesReadableJwt()
    {
        var token = CreateSut().GenerateToken(MakeUser());
        new JwtSecurityTokenHandler().CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public void GenerateToken_ContainsNameIdentifierClaim()
    {
        var user = MakeUser();
        var parsed = ParseToken(CreateSut().GenerateToken(user));
        parsed.Claims.Should().Contain(c =>
            c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
    }

    [Fact]
    public void GenerateToken_ContainsUsernameClaim()
    {
        var user = MakeUser();
        var parsed = ParseToken(CreateSut().GenerateToken(user));
        parsed.Claims.Should().Contain(c =>
            c.Type == ClaimTypes.Name && c.Value == "testuser");
    }

    [Fact]
    public void GenerateToken_ContainsRoleClaim()
    {
        var parsed = ParseToken(CreateSut().GenerateToken(MakeUser(role: "Teacher")));
        parsed.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Teacher");
    }

    [Fact]
    public void GenerateToken_WithTeacherId_ContainsTeacherIdClaim()
    {
        var tid = Guid.NewGuid();
        var parsed = ParseToken(CreateSut().GenerateToken(MakeUser(teacherId: tid)));
        parsed.Claims.Should().Contain(c => c.Type == "teacherId" && c.Value == tid.ToString());
    }

    [Fact]
    public void GenerateToken_WithoutTeacherId_OmitsTeacherIdClaim()
    {
        var parsed = ParseToken(CreateSut().GenerateToken(MakeUser(teacherId: null)));
        parsed.Claims.Should().NotContain(c => c.Type == "teacherId");
    }

    [Fact]
    public void GenerateToken_ValidatesWithCorrectKey()
    {
        var token = CreateSut().GenerateToken(MakeUser());
        var handler = new JwtSecurityTokenHandler();
        var prms = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = Issuer, ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret))
        };
        var act = () => handler.ValidateToken(token, prms, out _);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateToken_FailsValidationWithWrongKey()
    {
        var token = CreateSut().GenerateToken(MakeUser());
        var handler = new JwtSecurityTokenHandler();
        var prms = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, ValidateIssuer = false, ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("WrongKeyWrongKeyWrongKeyWrongKeyX"))
        };
        var act = () => handler.ValidateToken(token, prms, out _);
        act.Should().Throw<Exception>();
    }

    private static JwtSecurityToken ParseToken(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);
}
