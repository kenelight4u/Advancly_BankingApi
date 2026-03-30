using BankingApi.Application.Common.Exceptions;
using BankingApi.Application.Transactions.Commands;
using BankingApi.Domain.Enums;
using BankingApi.Infrastructure.Services;
using BankingApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BankingApi.Tests.Transactions;

public class TransferFundsHandlerTests
{
    // ── Shared arrange helpers ────────────────────────────────────────────────

    private static Mock<IFeeCalculator> FixedFee(decimal fee)
    {
        var mock = new Mock<IFeeCalculator>();
        mock.Setup(f => f.Calculate(It.IsAny<decimal>())).Returns(fee);
        return mock;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Happy-path tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Handle_ValidTransfer_DebitsSenderAndCreditsRecipientCorrectly()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "sender@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 100_000.00m);

        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "recipient@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 50_000.00m);

        var handler = new TransferFundsHandler(db, FixedFee(25.00m).Object);
        var command = new TransferFundsCommand(
            SenderId: sender.UserId,
            DestAccountNumber: recipient.AccountNumber,
            Amount: 10_000.00m,
            Narration: "Test transfer");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedSender = await db.Accounts.FindAsync(sender.Id);
        var updatedRecipient = await db.Accounts.FindAsync(recipient.Id);

        updatedSender!.Balance.Should().Be(100_000.00m - 10_025.00m);   // amount + fee
        updatedRecipient!.Balance.Should().Be(50_000.00m + 10_000.00m); // amount only

        result.Amount.Should().Be(10_000.00m);
        result.Fee.Should().Be(25.00m);
        result.TotalDebited.Should().Be(10_025.00m);
        result.Status.Should().Be(TransactionStatus.Completed);
    }

    [Fact]
    public async Task Handle_ValidTransfer_CreatesThreeLegsWithSharedReference()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 50_000.00m);
        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "r@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 0.00m);

        var handler = new TransferFundsHandler(db, FixedFee(10.00m).Object);
        var command = new TransferFundsCommand(
            sender.UserId, recipient.AccountNumber, 5_000.00m, "Leg test");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — exactly 3 legs
        var legs = await db.Transactions
            .Where(t => t.Reference == result.Reference)
            .ToListAsync();

        legs.Should().HaveCount(3);
        legs.Select(t => t.Reference).Distinct().Should().HaveCount(1);

        legs.Should().ContainSingle(t => t.Type == TransactionType.CustomerTransfer);
        legs.Should().ContainSingle(t => t.Type == TransactionType.FeeCapture);
        legs.Should().ContainSingle(t => t.Type == TransactionType.NGLDebit);
    }

    [Fact]
    public async Task Handle_ValidTransfer_FeeSettlesFromNglCreditToNglDebit()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        var (nglCredit, nglDebit, nglFee) = await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 50_000.00m);
        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "r@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 0.00m);

        const decimal fee = 25.00m;
        const decimal amount = 10_000.00m;

        var handler = new TransferFundsHandler(db, FixedFee(fee).Object);
        var command = new TransferFundsCommand(
            sender.UserId, recipient.AccountNumber, amount, null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — fee leg
        var feeLeg = await db.Transactions
            .FirstAsync(t => t.Type == TransactionType.FeeCapture);

        feeLeg.SourceAccountNumber.Should().Be(nglCredit.AccountNumber);
        feeLeg.DestAccountNumber.Should().Be(nglDebit.AccountNumber);
        feeLeg.Amount.Should().Be(fee);
        feeLeg.Fee.Should().Be(0.00m);
        feeLeg.TotalDebited.Should().Be(fee);

        // NGL Credit net balance: received (amount+fee), sent fee out → net = amount
        var updatedCredit = await db.Accounts.FindAsync(nglCredit.Id);
        updatedCredit!.Balance.Should().Be(amount); // 10000 stays as revenue
    }

    [Fact]
    public async Task Handle_ValidTransfer_NglDebitSendsAmountToRecipient()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        var (_, nglDebit, nglFee) = await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 50_000.00m);
        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "r@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 0.00m);

        const decimal amount = 5_000.00m;
        const decimal fee = 10.00m;

        var handler = new TransferFundsHandler(db, FixedFee(fee).Object);
        var command = new TransferFundsCommand(
            sender.UserId, recipient.AccountNumber, amount, null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — NGLDebit leg
        var nglDebitLeg = await db.Transactions
            .FirstAsync(t => t.Type == TransactionType.NGLDebit);

        nglDebitLeg.SourceAccountNumber.Should().Be(nglDebit.AccountNumber);
        nglDebitLeg.DestAccountNumber.Should().Be(recipient.AccountNumber);
        nglDebitLeg.Amount.Should().Be(amount);
        nglDebitLeg.Fee.Should().Be(0.00m);
        nglDebitLeg.TotalDebited.Should().Be(amount);

        var updatedRecipient = await db.Accounts.FindAsync(recipient.Id);
        updatedRecipient!.Balance.Should().Be(amount);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Failure / guard tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Handle_InsufficientBalance_ThrowsInsufficientFundsException()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 100.00m);   // only ₦100
        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "r@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 0.00m);

        var handler = new TransferFundsHandler(db, FixedFee(25.00m).Object);
        var command = new TransferFundsCommand(
            sender.UserId, recipient.AccountNumber, 5_000.00m, null);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InsufficientFundsException>()
            .WithMessage("*Insufficient*");
    }

    [Fact]
    public async Task Handle_SameAccountTransfer_ThrowsValidationException()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 50_000.00m);

        var handler = new TransferFundsHandler(db, FixedFee(25.00m).Object);
        var command = new TransferFundsCommand(
            sender.UserId,
            sender.AccountNumber,   // same account!
            1_000.00m,
            null);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*same account*");
    }

    [Fact]
    public async Task Handle_RecipientNotFound_ThrowsNotFoundException()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 50_000.00m);

        var handler = new TransferFundsHandler(db, FixedFee(10.00m).Object);
        var command = new TransferFundsCommand(
            sender.UserId,
            "9999999999",   // non-existent account
            1_000.00m,
            null);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Recipient*");
    }

    [Fact]
    public async Task Handle_SenderNotFound_ThrowsNotFoundException()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);

        // No customer seeded — sender UserId is random
        var handler = new TransferFundsHandler(db, FixedFee(10.00m).Object);
        var command = new TransferFundsCommand(
            Guid.NewGuid(),   // no matching account
            "0000000004",
            1_000.00m,
            null);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Sender*");
    }

    [Fact]
    public async Task Handle_NglCreditNotConfigured_ThrowsNotFoundException()
    {
        // Arrange — seed ONLY the NGL Debit account, not Credit
        await using var db = InMemoryDbHelper.CreateContext();

        var nglDebitUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Users.Add(new Domain.Entities.User
        {
            Id = nglDebitUserId,
            FirstName = "NGL",
            LastName = "Debit",
            Gender = Gender.System,
            Email = "ngl.debit@system.internal",
            Password = "x",
            Country = "Nigeria",
            CreatedAt = now
        });
        db.Accounts.Add(new Domain.Entities.Account
        {
            Id = Guid.NewGuid(),
            UserId = nglDebitUserId,
            AccountNumber = "0000000002",
            Balance = 1_000_000m,
            AccountType = AccountType.NGL,
            NglPoolType = NglPoolType.Debit,
            IsSystemAccount = true,
            Currency = "NGN",
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 50_000.00m);
        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "r@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 0.00m);

        var handler = new TransferFundsHandler(db, FixedFee(10.00m).Object);
        var command = new TransferFundsCommand(
            sender.UserId, recipient.AccountNumber, 1_000.00m, null);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*NGL Credit*");
    }

    [Fact]
    public async Task Handle_NglDebitNotConfigured_ThrowsNotFoundException()
    {
        // Arrange — seed ONLY the NGL Credit account, not Debit
        await using var db = InMemoryDbHelper.CreateContext();

        var nglCreditUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Users.Add(new Domain.Entities.User
        {
            Id = nglCreditUserId,
            FirstName = "NGL",
            LastName = "Credit",
            Gender = Gender.System,
            Email = "ngl.credit@system.internal",
            Password = "x",
            Country = "Nigeria",
            CreatedAt = now
        });
        db.Accounts.Add(new Domain.Entities.Account
        {
            Id = Guid.NewGuid(),
            UserId = nglCreditUserId,
            AccountNumber = "0000000001",
            Balance = 0m,
            AccountType = AccountType.NGL,
            NglPoolType = NglPoolType.Credit,
            IsSystemAccount = true,
            Currency = "NGN",
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 50_000.00m);
        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "r@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 0.00m);

        var handler = new TransferFundsHandler(db, FixedFee(10.00m).Object);
        var command = new TransferFundsCommand(
            sender.UserId, recipient.AccountNumber, 1_000.00m, null);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*NGL Debit*");
    }
}