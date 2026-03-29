namespace BankingApi.Application.Transactions.Commands;

/// <summary>
/// Command to transfer funds between two customer accounts.
/// SenderId is ALWAYS sourced from the authenticated JWT claim —
/// it is never accepted from the HTTP request body.
/// </summary>
public record TransferFundsCommand(
    Guid SenderId,            // from JWT claim
    string DestAccountNumber,
    decimal Amount,
    string? Narration
);

public record TransferFundsResult(
    string Reference,
    decimal Amount,
    decimal Fee,
    decimal TotalDebited,
    string RecipientAccountNumber,
    string SenderAccountNumber,
    string Status,
    DateTime TransactedAt
);