namespace BankingApi.Application.Auth.Commands;

/// <summary>
/// Command to register a new customer user and auto-generate their account.
/// SenderId is never accepted from the request body — only from the JWT claim
/// on authenticated endpoints. Registration is public, so no SenderId here.
/// </summary>
public record RegisterUserCommand(
    string FirstName,
    string? MiddleName,
    string LastName,
    string Gender,
    string Email,
    string Password,
    string BVN,
    string? Address,
    string? State,
    string Country
);

public record RegisterUserResult(
    Guid UserId,
    string FullName,
    string Email,
    string AccountNumber,
    string Currency,
    string AccountType,
    DateTime CreatedAt
);