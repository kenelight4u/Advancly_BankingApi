using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Infrastructure.Services;

public interface IAccountNumberGenerator
{
    /// <summary>
    /// Reads MAX(AccountNumber) across ALL accounts (Customer + NGL),
    /// increments by 1, and returns a zero-padded 10-digit string.
    /// NGL seed accounts consume 0000000001 and 0000000002, so the first
    /// regular customer account will always receive 0000000003.
    /// </summary>
    Task<string> GenerateAsync(CancellationToken ct = default);
}

public class AccountNumberGenerator : IAccountNumberGenerator
{
    private readonly BankingDbContext _db;

    public AccountNumberGenerator(BankingDbContext db) => _db = db;

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        // MaxAsync returns null when the table is empty — default to "0000000000"
        var max = await _db.Accounts
                            .MaxAsync(a => (string?)a.AccountNumber, ct);

        var next = long.Parse(max ?? "0000000000") + 1L;

        return next.ToString("D10");
    }
}