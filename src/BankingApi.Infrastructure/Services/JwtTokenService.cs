using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BankingApi.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BankingApi.Infrastructure.Services;

public interface IJwtTokenService
{
    TokenResult GenerateToken(User user);
}

public record TokenResult(string Token, DateTime ExpiresAt);

public class JwtTokenService : IJwtTokenService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly int _expiresInMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");

        _key = section["Key"]
                            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        _issuer = section["Issuer"]
                            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        _expiresInMinutes = int.Parse(section["ExpiresInMinutes"]
                            ?? throw new InvalidOperationException("Jwt:ExpiresInMinutes is not configured."));
    }

    public TokenResult GenerateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_expiresInMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,        user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email,      user.Email),
            new Claim(JwtRegisteredClaimNames.GivenName,  user.FirstName),
            new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti,        Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _issuer,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new TokenResult(
            Token: new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt: expiresAt);
    }
}