namespace BankingApi.Application.Auth.Commands;

/// <summary>
/// Command to authenticate a user with email + password.
/// Returns a signed JWT token and its expiry timestamp.
/// </summary>
public record LoginCommand(
    string Email,
    string Password
);

public record LoginResult(
    string Token,
    DateTime ExpiresAt,
    Guid UserId,
    string FullName,
    string Email
);