using BankingApi.Application.Transactions.Queries;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Tests.Helpers;
using FluentAssertions;

namespace BankingApi.Tests.Transactions;

public class GetTransactionHistoryHandlerTests
{
    // ── Helper — seeds a completed transfer as 3 legs ─────────────────────────

    private static async Task SeedCompletedTransferAsync(
        Infrastructure.Persistence.BankingDbContext db,
        string senderNumber,
        string recipientNumber,
        string nglCreditNumber,
        string nglDebitNumber,
        decimal amount,
        decimal fee,
        string reference)
    {
        var now = DateTime.UtcNow;
        var totalDebited = amount + fee;

        db.Transactions.AddRange(
            new Transaction
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                SourceAccountNumber = senderNumber,
                DestAccountNumber = nglCreditNumber,
                Amount = amount,
                Fee = fee,
                TotalDebited = totalDebited,
                Narration = "Test transfer",
                Type = TransactionType.CustomerTransfer,
                Status = TransactionStatus.Completed,
                CreatedAt = now
            },
            new Transaction
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                SourceAccountNumber = nglCreditNumber,
                DestAccountNumber = nglDebitNumber,
                Amount = fee,
                Fee = 0m,
                TotalDebited = fee,
                Narration = "Fee settlement",
                Type = TransactionType.FeeCapture,
                Status = TransactionStatus.Completed,
                CreatedAt = now
            },
            new Transaction
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                SourceAccountNumber = nglDebitNumber,
                DestAccountNumber = recipientNumber,
                Amount = amount,
                Fee = 0m,
                TotalDebited = amount,
                Narration = "Test transfer",
                Type = TransactionType.NGLDebit,
                Status = TransactionStatus.Completed,
                CreatedAt = now
            });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ValidUser_ReturnsOnlyOwnTransactions()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        var (nglCredit, nglDebit, nglFee) = await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 500_000.00m);
        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "r@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 0.00m);
        var (_, other) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Bob", email: "b@test.com",
            accountNumber: "0000000005", bvn: "33333333333", balance: 0.00m);

        // Seed one transfer sender→recipient
        await SeedCompletedTransferAsync(db,
            sender.AccountNumber, recipient.AccountNumber,
            nglCredit.AccountNumber, nglDebit.AccountNumber,
            10_000m, 25m, "REF001");

        // Seed another transfer other→recipient (should NOT appear for sender)
        await SeedCompletedTransferAsync(db,
            other.AccountNumber, recipient.AccountNumber,
            nglCredit.AccountNumber, nglDebit.AccountNumber,
            5_000m, 10m, "REF002");

        var handler = new GetTransactionHistoryHandler(db);
        var query = new GetTransactionHistoryQuery(
            UserId: sender.UserId,
            PageNumber: 1,
            PageSize: 20);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — sender only sees REF001
        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].Reference.Should().Be("REF001");
    }

    [Fact]
    public async Task Handle_ValidUser_GroupsLegsByReference()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        var (nglCredit, nglDebit, nglFee) = await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 500_000.00m);
        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "r@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 0.00m);

        await SeedCompletedTransferAsync(db,
            sender.AccountNumber, recipient.AccountNumber,
            nglCredit.AccountNumber, nglDebit.AccountNumber,
            10_000m, 25m, "REFGROUP01");

        var handler = new GetTransactionHistoryHandler(db);
        var query = new GetTransactionHistoryQuery(sender.UserId, 1, 20);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — one group with three legs
        result.Transactions.Should().HaveCount(1);

        var group = result.Transactions[0];
        group.Reference.Should().Be("REFGROUP01");
        group.Legs.Should().HaveCount(3);
        group.Legs.Should().ContainSingle(l =>
            l.Type == TransactionType.CustomerTransfer);
        group.Legs.Should().ContainSingle(l =>
            l.Type == TransactionType.FeeCapture);
        group.Legs.Should().ContainSingle(l =>
            l.Type == TransactionType.NGLDebit);

        // Group-level amounts driven by CustomerTransfer leg
        group.Amount.Should().Be(10_000m);
        group.Fee.Should().Be(25m);
        group.TotalDebited.Should().Be(10_025m);
    }

    [Fact]
    public async Task Handle_ValidUser_ReturnsPaginatedResults()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        var (nglCredit, nglDebit, nglFee) = await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 500_000.00m);
        var (_, recipient) = await InMemoryDbHelper.SeedCustomerAsync(
            db, firstName: "Jane", email: "r@test.com",
            accountNumber: "0000000004", bvn: "22222222222", balance: 0.00m);

        // Seed 5 distinct transfers
        for (var i = 1; i <= 5; i++)
        {
            await SeedCompletedTransferAsync(db,
                sender.AccountNumber, recipient.AccountNumber,
                nglCredit.AccountNumber, nglDebit.AccountNumber,
                1_000m * i, 10m, $"REFPAGE{i:D2}");
        }

        var handler = new GetTransactionHistoryHandler(db);

        // Act — page 1 of 2 (pageSize = 3)
        var page1 = await handler.Handle(
            new GetTransactionHistoryQuery(sender.UserId, 1, 3),
            CancellationToken.None);

        // Act — page 2 of 2
        var page2 = await handler.Handle(
            new GetTransactionHistoryQuery(sender.UserId, 2, 3),
            CancellationToken.None);

        // Assert
        page1.TotalCount.Should().Be(5);
        page1.TotalPages.Should().Be(2);
        page1.Transactions.Should().HaveCount(3);

        page2.TotalCount.Should().Be(5);
        page2.TotalPages.Should().Be(2);
        page2.Transactions.Should().HaveCount(2);

        // No overlap between pages
        var page1Refs = page1.Transactions.Select(t => t.Reference).ToHashSet();
        var page2Refs = page2.Transactions.Select(t => t.Reference).ToHashSet();
        page1Refs.Should().NotIntersectWith(page2Refs);
    }

    [Fact]
    public async Task Handle_NoTransactions_ReturnsEmptyList()
    {
        // Arrange
        await using var db = InMemoryDbHelper.CreateContext();
        await InMemoryDbHelper.SeedNglAccountsAsync(db);

        var (_, sender) = await InMemoryDbHelper.SeedCustomerAsync(
            db, email: "s@test.com", accountNumber: "0000000003",
            bvn: "11111111111", balance: 50_000.00m);

        // No transactions seeded
        var handler = new GetTransactionHistoryHandler(db);
        var query = new GetTransactionHistoryQuery(sender.UserId, 1, 20);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Transactions.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }
}