using FluentAssertions;
using Microsoft.Extensions.Configuration;
using UniScheduler.Application.Features.Auth.Commands;
using UniScheduler.Domain.Entities;
using UniScheduler.Infrastructure.Auth;
using UniScheduler.IntegrationTests.Helpers;
using Xunit;

namespace UniScheduler.IntegrationTests.Application;

public class AuthCommandTests
{
    private static JwtService MakeJwt()
    {
        var data = new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "TestSecretKeyWith32+CharactersForHmac",
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
            ["Jwt:ExpiryDays"] = "7"
        };
        return new JwtService(new ConfigurationBuilder().AddInMemoryCollection(data).Build().GetSection("Jwt"));
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        using var db = DbContextFactory.Create();
        var user = new AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
            Role = "Admin"
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        var result = await new LoginCommandHandler(db, MakeJwt())
            .Handle(new LoginCommand("admin", "secret123"), CancellationToken.None);

        result.Token.Should().NotBeNullOrEmpty();
        result.Role.Should().Be("Admin");
        result.UserId.Should().Be(user.Id);
        result.TeacherId.Should().BeNull();
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        using var db = DbContextFactory.Create();
        db.AppUsers.Add(new AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct"),
            Role = "Admin"
        });
        await db.SaveChangesAsync();

        var act = async () => await new LoginCommandHandler(db, MakeJwt())
            .Handle(new LoginCommand("admin", "wrong"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public async Task Login_UnknownUser_ThrowsUnauthorized()
    {
        using var db = DbContextFactory.Create();
        var act = async () => await new LoginCommandHandler(db, MakeJwt())
            .Handle(new LoginCommand("nobody", "pass"), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Login_WithTeacherId_IncludedInResult()
    {
        using var db = DbContextFactory.Create();
        var tid = Guid.NewGuid();
        db.AppUsers.Add(new AppUser
        {
            Username = "teacher1",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = "Teacher",
            TeacherId = tid
        });
        await db.SaveChangesAsync();

        var result = await new LoginCommandHandler(db, MakeJwt())
            .Handle(new LoginCommand("teacher1", "pass"), CancellationToken.None);

        result.TeacherId.Should().Be(tid);
        result.Role.Should().Be("Teacher");
    }
}
