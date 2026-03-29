using BankingApi.Application.Common.Exceptions;
using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Application.Accounts.Commands;

/// <summary>
/// Handles UpdateAccountCommand.
/// Applies PATCH semantics — only non-null fields are updated.
/// Immutable fields (Email, Password, BVN, AccountNumber, Balance,
/// AccountType, IsSystemAccount) are never touched here.
/// Ownership is enforced: UserId from JWT must match the record.
/// </summary>
public class UpdateAccountHandler
{
    private readonly BankingDbContext _db;

    public UpdateAccountHandler(BankingDbContext db) => _db = db;

    public async Task<UpdateAccountResult> Handle(
        UpdateAccountCommand cmd,
        CancellationToken ct)
    {
        // Load user — enforces ownership via UserId from JWT claim
        var user = await _db.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u =>
                u.Id == cmd.UserId &&
                u.Account != null &&
                u.Account.IsSystemAccount == false, ct)
            ?? throw new NotFoundException(
                $"Account not found for user '{cmd.UserId}'.");

        var account = user.Account!;

        // ── Apply only provided (non-null) fields ─────────────────────────────

        if (cmd.FirstName is not null)
            user.FirstName = cmd.FirstName.Trim();

        if (cmd.MiddleName is not null)
            user.MiddleName = cmd.MiddleName.Trim();

        if (cmd.LastName is not null)
            user.LastName = cmd.LastName.Trim();

        if (cmd.Gender is not null)
            user.Gender = cmd.Gender;

        if (cmd.Address is not null)
            user.Address = cmd.Address.Trim();

        if (cmd.State is not null)
            user.State = cmd.State.Trim();

        if (cmd.Country is not null)
            user.Country = cmd.Country.Trim();

        // Stamp the account's UpdatedAt on every successful update
        account.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new UpdateAccountResult(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            Gender: user.Gender,
            Address: user.Address,
            State: user.State,
            Country: user.Country,
            AccountNumber: account.AccountNumber,
            AccountUpdatedAt: account.UpdatedAt);
    }
}