using BankingApi.Application.Auth.Commands;
using BankingApi.Application.Common.Exceptions;
using BankingApi.Infrastructure.Services;
using BankingApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace BankingApi.Tests.Auth;

public class LoginHandlerTests
{
    private static IJwtTokenService BuildJwtService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TestSuperSecretKeyForUnitTestsMustBe32Chars!!",
                ["Jwt:Issuer"] = "BankingApiTest",
                ["Jwt:Audience"] = "BankingApiUsers",
                ["Jwt:ExpiresInMinutes"] = "60"
            })
            .Build();

        return new JwtTokenService(config);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsJwtToken()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "john@test.com",
            accountNumber: "0000000003", bvn: "12345678901");

        var handler = new LoginHandler(db, BuildJwtService());
        var command = new LoginCommand("john@test.com", "Test@1234");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.Email.Should().Be("john@test.com");
        result.FullName.Should().Contain("John");
    }

    [Fact]
    public async Task Handle_InvalidPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "john@test.com",
            accountNumber: "0000000003", bvn: "12345678901");

        var handler = new LoginHandler(db, BuildJwtService());
        var command = new LoginCommand("john@test.com", "WrongPassword!");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        // No users seeded

        var handler = new LoginHandler(db, BuildJwtService());
        var command = new LoginCommand("ghost@test.com", "Test@1234");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid email or password*");
    }
}
