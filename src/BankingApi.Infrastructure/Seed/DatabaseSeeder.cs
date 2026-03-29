using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BC = BCrypt.Net.BCrypt;

namespace BankingApi.Infrastructure.Seed;

/// <summary>
/// Idempotent seeder — checks existence before every insert.
/// Execution order matters:
///   1. NGL Credit user + account
///   2. NGL Debit  user + account
///   3. NGL Fee    user + account
///   4. Test customer John Doe
///   5. Test customer Jane Doe
/// Only runs in Development (enforced by the caller in Program.cs).
/// </summary>
public class DatabaseSeeder
{
    private readonly BankingDbContext _db;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(BankingDbContext db, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Apply any pending migrations before seeding
        await _db.Database.MigrateAsync(ct);

        await SeedNglCreditAsync(ct);
        await SeedNglDebitAsync(ct);
        await SeedNglFeeAsync(ct);
        await SeedJohnDoeAsync(ct);
        await SeedJaneDoeAsync(ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Database seeding completed successfully.");
    }

    private async Task SeedNglCreditAsync(CancellationToken ct)
    {
        const string email = "ngl.credit@system.internal";
        const string accountNumber = "0000000001";

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            _logger.LogInformation("NGL Credit user already exists — skipping.");
            return;
        }

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = userId,
            FirstName = "NGL",
            LastName = "Credit",
            Gender = Gender.System,
            Email = email,
            Password = BC.HashPassword("System@NGL1", workFactor: 12),
            Country = "Nigeria",
            CreatedAt = now
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = accountNumber,
            BVN = null,
            Balance = 0.00m,
            Currency = "NGN",
            AccountType = AccountType.NGL,
            NglPoolType = NglPoolType.Credit,
            IsSystemAccount = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        _db.Accounts.Add(account);

        _logger.LogInformation(
            "Seeded NGL Credit user ({Email}) with account {AccountNumber}.",
            email, accountNumber);
    }

    private async Task SeedNglDebitAsync(CancellationToken ct)
    {
        const string email = "ngl.debit@system.internal";
        const string accountNumber = "0000000002";

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            _logger.LogInformation("NGL Debit user already exists — skipping.");
            return;
        }

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = userId,
            FirstName = "NGL",
            LastName = "Debit",
            Gender = Gender.System,
            Email = email,
            Password = BC.HashPassword("System@NGL2", workFactor: 12),
            Country = "Nigeria",
            CreatedAt = now
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = accountNumber,
            BVN = null,
            Balance = 1_000_000_000.00m,   // Pre-funded to support outbound legs
            Currency = "NGN",
            AccountType = AccountType.NGL,
            NglPoolType = NglPoolType.Debit,
            IsSystemAccount = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        _db.Accounts.Add(account);

        _logger.LogInformation(
            "Seeded NGL Debit user ({Email}) with account {AccountNumber}.",
            email, accountNumber);
    }

    private async Task SeedNglFeeAsync(CancellationToken ct)
    {
        const string email = "ngl.fee@system.internal";
        const string accountNumber = "0000000003";

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            _logger.LogInformation("NGL Fee user already exists — skipping.");
            return;
        }

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = userId,
            FirstName = "NGL",
            LastName = "Fee",
            Gender = Gender.System,
            Email = email,
            Password = BC.HashPassword("System@NGL3", workFactor: 12),
            Country = "Nigeria",
            CreatedAt = now
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = accountNumber,
            BVN = null,
            Balance = 0.00m,
            Currency = "NGN",
            AccountType = AccountType.NGL,
            NglPoolType = NglPoolType.Fee,
            IsSystemAccount = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        _db.Accounts.Add(account);

        _logger.LogInformation(
            "Seeded NGL Fee user ({Email}) with account {AccountNumber}.",
            email, accountNumber);
    }

    private async Task SeedJohnDoeAsync(CancellationToken ct)
    {
        const string email = "john@test.com";
        const string accountNumber = "0000000004";

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            _logger.LogInformation("John Doe already exists — skipping.");
            return;
        }

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = userId,
            FirstName = "John",
            LastName = "Doe",
            Gender = Gender.Male,
            Email = email,
            Password = BC.HashPassword("Test@1234", workFactor: 12),
            Country = "Nigeria",
            CreatedAt = now
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = accountNumber,
            BVN = "12345678901",
            Balance = 500_000.00m,
            Currency = "NGN",
            AccountType = AccountType.Customer,
            NglPoolType = null,
            IsSystemAccount = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        _db.Accounts.Add(account);

        _logger.LogInformation(
            "Seeded customer John Doe ({Email}) with account {AccountNumber}.",
            email, accountNumber);
    }

    // ── Test Customer — Jane Doe ──────────────────────────────────────────────

    private async Task SeedJaneDoeAsync(CancellationToken ct)
    {
        const string email = "jane@test.com";
        const string accountNumber = "0000000005";

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            _logger.LogInformation("Jane Doe already exists — skipping.");
            return;
        }

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = userId,
            FirstName = "Jane",
            LastName = "Doe",
            Gender = Gender.Female,
            Email = email,
            Password = BC.HashPassword("Test@1234", workFactor: 12),
            Country = "Nigeria",
            CreatedAt = now
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = accountNumber,
            BVN = "98765432100",
            Balance = 100_000.00m,
            Currency = "NGN",
            AccountType = Domain.Enums.AccountType.Customer,
            NglPoolType = null,
            IsSystemAccount = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        _db.Accounts.Add(account);

        _logger.LogInformation(
            "Seeded customer Jane Doe ({Email}) with account {AccountNumber}.",
            email, accountNumber);
    }
}