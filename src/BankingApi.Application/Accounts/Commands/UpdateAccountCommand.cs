namespace BankingApi.Application.Accounts.Commands;

/// <summary>
/// Command to update mutable profile fields for the authenticated user.
/// UserId sourced from JWT claim. Fields left null are not updated (PATCH semantics).
/// Password and Email are intentionally excluded — separate flows handle those.
/// BVN, AccountNumber, Balance, AccountType are immutable after creation.
/// </summary>
public record UpdateAccountCommand(
    Guid UserId,       // from JWT claim — never request body
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Gender,
    string? Address,
    string? State,
    string? Country
);

public record UpdateAccountResult(
    Guid UserId,
    string FullName,
    string Email,
    string Gender,
    string? Address,
    string? State,
    string Country,
    string AccountNumber,
    DateTime AccountUpdatedAt
);