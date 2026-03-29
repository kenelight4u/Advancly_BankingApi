namespace BankingApi.Application.Transactions.Queries;

/// <summary>
/// Query to retrieve paginated transaction history for the authenticated user.
/// Only transactions where the user's AccountNumber appears as
/// SourceAccountNumber or DestAccountNumber are returned.
/// Legs are grouped by Reference so each transfer surfaces as one record
/// containing all three legs.
/// </summary>
public record GetTransactionHistoryQuery(
    Guid UserId,
    int PageNumber = 1,
    int PageSize = 20
);

/// <summary>A single transaction leg.</summary>
public record TransactionLegDto(
    Guid Id,
    string Type,             
    string SourceAccountNumber,
    string DestAccountNumber,
    decimal Amount,
    decimal Fee,
    decimal TotalDebited,
    string? Narration,
    string Status,
    DateTime CreatedAt
);

/// <summary>
/// One logical transfer — groups all three legs under a shared Reference.
/// </summary>
public record TransactionGroupDto(
    string Reference,
    decimal Amount,        
    decimal Fee,          
    decimal TotalDebited,  
    string Status,        
    DateTime CreatedAt,     
    IReadOnlyList<TransactionLegDto> Legs
);

/// <summary>Paginated wrapper returned to the controller.</summary>
public record GetTransactionHistoryResult(
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<TransactionGroupDto> Transactions
);