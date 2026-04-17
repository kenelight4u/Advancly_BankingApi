using BankingApi.Application.Auth.Commands;
using BankingApi.Application.Common.Exceptions;
using BankingApi.Domain.Enums;
using BankingApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Tests.Auth;

public class RegisterUserHandlerTests
{
    private static RegisterUserCommand ValidCommand(
        string email = "new@test.com",
        string bvn = "99999999999") =>
        new(
            FirstName: "Alice",
            MiddleName: null,
            LastName: "Smith",
            Gender: Gender.Female,
            Email: email,
            Password: "Test@1234",
            BVN: bvn,
            Address: "12 Lagos Street",
            State: "Lagos",
            Country: "Nigeria");

    [Fact]
    public async Task Handle_ValidRegistration_CreatesUserAndCustomerAccount()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);
        var handler = new RegisterUserHandler(db,
            new Infrastructure.Services.AccountNumberGenerator(db));

        // Act
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        var user = await db.Users.FindAsync(result.UserId);
        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == result.UserId);

        user.Should().NotBeNull();
        account.Should().NotBeNull();
        account!.AccountType.Should().Be(AccountType.Customer);
        result.Email.Should().Be("new@test.com");
    }

    [Fact]
    public async Task Handle_ValidRegistration_PasswordIsHashed()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);
        var handler = new RegisterUserHandler(db,
            new Infrastructure.Services.AccountNumberGenerator(db));

        // Act
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        var user = await db.Users.FindAsync(result.UserId);
        user!.Password.Should().NotBe("Test@1234");
        BCrypt.Net.BCrypt.Verify("Test@1234", user.Password).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRegistration_AccountNumberIsGenerated()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db); // seeds 0000000001 + 0000000003

        var handler = new RegisterUserHandler(db,
            new Infrastructure.Services.AccountNumberGenerator(db));

        // Act
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert — first customer account must follow the 3 NGL accounts
        result.AccountNumber.Should().Be("0000000004");
    }

    [Fact]
    public async Task Handle_ValidRegistration_AccountTypeIsCustomer()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);
        var handler = new RegisterUserHandler(db,
            new Infrastructure.Services.AccountNumberGenerator(db));

        // Act
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.AccountType.Should().Be(AccountType.Customer);

        var account = await db.Accounts
            .FirstAsync(a => a.AccountNumber == result.AccountNumber);
        account.IsSystemAccount.Should().BeFalse();
        account.NglPoolType.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsValidationException()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);
        await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "duplicate@test.com",
            accountNumber: "0000000004", bvn: "11111111111");

        var handler = new RegisterUserHandler(db,
            new Infrastructure.Services.AccountNumberGenerator(db));

        var command = ValidCommand(email: "duplicate@test.com", bvn: "99999999999");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("email");
        exception.Which.Errors["email"].Should()
            .Contain(message => message.Contains("email address", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_DuplicateBVN_ThrowsValidationException()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);
        await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "existing@test.com",
            accountNumber: "0000000004", bvn: "12345678901");

        var handler = new RegisterUserHandler(db,
            new Infrastructure.Services.AccountNumberGenerator(db));

        var command = ValidCommand(email: "new@test.com", bvn: "12345678901"); // same BVN

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("bvn");
        exception.Which.Errors["bvn"].Should()
            .Contain(message => message.Contains("BVN", StringComparison.OrdinalIgnoreCase));
    }
}