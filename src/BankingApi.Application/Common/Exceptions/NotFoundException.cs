namespace BankingApi.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist in the database.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message) { }

    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with key '{key}' was not found.") { }

    public NotFoundException(string message, Exception inner)
        : base(message, inner) { }
}