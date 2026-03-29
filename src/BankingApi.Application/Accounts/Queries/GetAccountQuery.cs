namespace BankingApi.Application.Accounts.Queries;

/// <summary>
/// Query to retrieve the authenticated user's account profile.
/// UserId is always sourced from the JWT claim — never the request body.
/// </summary>
public record GetAccountQuery(Guid UserId);

public record GetAccountResult(
    Guid UserId,
    string FullName,
    string Email,
    string Gender,
    string? Address,
    string? State,
    string Country,
    string AccountNumber,
    string MaskedBVN,         // "XXXXXXX" + last 4 digits — never raw
    decimal Balance,
    string Currency,
    string AccountType,
    DateTime AccountCreatedAt,
    DateTime AccountUpdatedAt,
    DateTime UserCreatedAt
);