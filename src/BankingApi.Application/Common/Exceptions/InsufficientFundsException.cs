namespace BankingApi.Application.Common.Exceptions;

/// <summary>
/// Thrown when a sender's account balance is less than the total amount
/// required to complete a transfer (Amount + Fee).
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class InsufficientFundsException : Exception
{
    public decimal Available { get; }
    public decimal Required { get; }

    public InsufficientFundsException(string message)
        : base(message) { }

    public InsufficientFundsException(decimal available, decimal required)
        : base($"Insufficient funds. Required: {required:N2} NGN, Available: {available:N2} NGN.")
    {
        Available = available;
        Required = required;
    }

    public InsufficientFundsException(string message, Exception inner)
        : base(message, inner) { }
}