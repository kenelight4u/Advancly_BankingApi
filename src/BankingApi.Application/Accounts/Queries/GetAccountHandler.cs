using BankingApi.Application.Common.Exceptions;
using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Application.Accounts.Queries;

/// <summary>
/// Handles GetAccountQuery.
/// Returns the authenticated user's profile + account details.
/// BVN is always masked — never returned raw.
/// System accounts are never reachable via this query because
/// the UserId comes from the JWT of a logged-in customer.
/// </summary>
public class GetAccountHandler
{
    private readonly BankingDbContext _db;

    public GetAccountHandler(BankingDbContext db) => _db = db;

    public async Task<GetAccountResult> Handle(
        GetAccountQuery query,
        CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u =>
                u.Id == query.UserId &&
                u.Account != null &&
                u.Account.IsSystemAccount == false, ct)
            ?? throw new NotFoundException(
                $"Account not found for user '{query.UserId}'.");

        var account = user.Account!;

        return new GetAccountResult(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            Gender: user.Gender,
            Address: user.Address,
            State: user.State,
            Country: user.Country,
            AccountNumber: account.AccountNumber,
            MaskedBVN: MaskBvn(account.BVN),
            Balance: account.Balance,
            Currency: account.Currency,
            AccountType: account.AccountType,
            AccountCreatedAt: account.CreatedAt,
            AccountUpdatedAt: account.UpdatedAt,
            UserCreatedAt: user.CreatedAt);
    }

    // ── BVN masking ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns "XXXXXXX" + last 4 digits.
    /// If BVN is null or shorter than 4 characters, returns "XXXXXXXXXXX".
    /// </summary>
    private static string MaskBvn(string? bvn)
    {
        if (string.IsNullOrWhiteSpace(bvn) || bvn.Length < 4)
            return "XXXXXXXXXXX";

        return $"XXXXXXX{bvn[^4..]}";
    }
}