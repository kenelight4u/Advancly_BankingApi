using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BankingApi.Tests.Helpers;

/// <summary>
/// Provides a fresh EF Core InMemory BankingDbContext for each test.
/// Seeds NGL Credit and NGL Debit accounts by default so every test
/// that exercises the transfer flow has the required system accounts.
/// </summary>
public static class InMemoryDbHelper
{
    public static BankingDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BankingDbContext(options);
    }

    /// <summary>
    /// Seeds the two required NGL system users + accounts.
    /// Call this in the Arrange phase of any test that touches transfers.
    /// Returns (nglCreditAccount, nglDebitAccount) for assertion convenience.
    /// </summary>
    public static async Task<(Account NglCredit, Account NglDebit, Account NglFee)>
        SeedNglAccountsAsync(BankingDbContext db)
    {
        var now = DateTime.UtcNow;

        // ── NGL Credit user + account ─────────────────────────────────────────
        var nglCreditUserId = Guid.NewGuid();
        var nglCreditUser = new User
        {
            Id = nglCreditUserId,
            FirstName = "NGL",
            LastName = "Credit",
            Gender = Gender.System,
            Email = "ngl.credit@system.internal",
            Password = "hashed",
            Country = "Nigeria",
            CreatedAt = now
        };
        var nglCredit = new Account
        {
            Id = Guid.NewGuid(),
            UserId = nglCreditUserId,
            AccountNumber = "0000000001",
            BVN = null,
            Balance = 0.00m,
            Currency = "NGN",
            AccountType = AccountType.NGL,
            NglPoolType = NglPoolType.Credit,
            IsSystemAccount = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        // ── NGL Debit user + account ──────────────────────────────────────────
        var nglDebitUserId = Guid.NewGuid();
        var nglDebitUser = new User
        {
            Id = nglDebitUserId,
            FirstName = "NGL",
            LastName = "Debit",
            Gender = Gender.System,
            Email = "ngl.debit@system.internal",
            Password = "hashed",
            Country = "Nigeria",
            CreatedAt = now
        };
        var nglDebit = new Account
        {
            Id = Guid.NewGuid(),
            UserId = nglDebitUserId,
            AccountNumber = "0000000002",
            BVN = null,
            Balance = 1_000_000.00m,
            Currency = "NGN",
            AccountType = AccountType.NGL,
            NglPoolType = NglPoolType.Debit,
            IsSystemAccount = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        // ── NGL Fee user + account ──────────────────────────────────────────
        var nglFeeUserId = Guid.NewGuid();
        var nglFeeUser = new User
        {
            Id = nglFeeUserId,
            FirstName = "NGL",
            LastName = "Fee",
            Gender = Gender.System,
            Email = "ngl.fee@system.internal",
            Password = "hashed",
            Country = "Nigeria",
            CreatedAt = now
        };
        var nglFee = new Account
        {
            Id = Guid.NewGuid(),
            UserId = nglFeeUserId,
            AccountNumber = "0000000003",
            BVN = null,
            Balance = 0.00m,
            Currency = "NGN",
            AccountType = AccountType.NGL,
            NglPoolType = NglPoolType.Fee,
            IsSystemAccount = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.AddRange(nglCreditUser, nglDebitUser, nglFeeUser);
        db.Accounts.AddRange(nglCredit, nglDebit, nglFee);
        await db.SaveChangesAsync();

        return (nglCredit, nglDebit, nglFee);
    }

    /// <summary>
    /// Seeds a customer user + account and returns both entities.
    /// </summary>
    public static async Task<(User User, Account Account)> SeedCustomerAsync(
        BankingDbContext db,
        string firstName = "John",
        string lastName = "Doe",
        string email = "john@test.com",
        string accountNumber = "0000000004",
        string bvn = "12345678901",
        decimal balance = 500_000.00m)
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            FirstName = firstName,
            LastName = lastName,
            Gender = Gender.Male,
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("Test@1234", workFactor: 4),
            Country = "Nigeria",
            CreatedAt = now
        };
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = accountNumber,
            BVN = bvn,
            Balance = balance,
            Currency = "NGN",
            AccountType = AccountType.Customer,
            NglPoolType = null,
            IsSystemAccount = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(user);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        return (user, account);
    }
}
