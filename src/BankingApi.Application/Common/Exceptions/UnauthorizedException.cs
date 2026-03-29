namespace BankingApi.Application.Common.Exceptions;

/// <summary>
/// Thrown when authentication fails — bad credentials, missing token,
/// or a user attempting to access another user's resource.
/// Maps to HTTP 401 Unauthorized.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Unauthorized.")
        : base(message) { }

    public UnauthorizedException(string message, Exception inner)
        : base(message, inner) { }
}