using BankingApi.Application.Common.Exceptions;
using BankingApi.Infrastructure.Persistence;
using BankingApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using BC = BCrypt.Net.BCrypt;

namespace BankingApi.Application.Auth.Commands;

/// <summary>
/// Handles LoginCommand.
/// Validates credentials and returns a signed JWT token.
/// Deliberately uses a generic error message for both "user not found"
/// and "wrong password" to prevent user enumeration attacks.
/// </summary>
public class LoginHandler
{
    private readonly BankingDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginHandler(BankingDbContext db, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResult> Handle(
        LoginCommand cmd,
        CancellationToken ct)
    {
        // Normalize email before lookup
        var email = cmd.Email.Trim().ToLowerInvariant();

        // Only Customer accounts can log in — system accounts are excluded
        var user = await _db.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u =>
                u.Email == email &&
                (u.Account == null ||
                 u.Account.IsSystemAccount == false), ct);

        // Generic error prevents user enumeration
        if (user is null || !BC.Verify(cmd.Password, user.Password))
            throw new UnauthorizedException(
                "Invalid email or password.");

        var tokenResult = _jwtTokenService.GenerateToken(user);

        return new LoginResult(
            Token: tokenResult.Token,
            ExpiresAt: tokenResult.ExpiresAt,
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email);
    }
}