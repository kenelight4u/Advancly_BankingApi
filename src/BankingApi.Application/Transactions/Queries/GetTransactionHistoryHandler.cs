using BankingApi.Application.Common.Exceptions;
using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Application.Transactions.Queries;

/// <summary>
/// Handles GetTransactionHistoryQuery.
///
/// Returns only transactions where the authenticated user's AccountNumber
/// appears as SourceAccountNumber OR DestAccountNumber — across all legs.
///
/// Groups legs by Reference so each logical transfer surfaces as one
/// TransactionGroupDto containing all three legs (CustomerTransfer,
/// FeeCapture, NGLDebit).
///
/// Supports pagination: pageNumber (1-based) and pageSize (1–100).
/// </summary>
public class GetTransactionHistoryHandler
{
    private readonly BankingDbContext _db;

    public GetTransactionHistoryHandler(BankingDbContext db) => _db = db;

    public async Task<GetTransactionHistoryResult> Handle(
        GetTransactionHistoryQuery query,
        CancellationToken ct)
    {
        // ── Resolve the authenticated user's account number ───────────────────
        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.UserId == query.UserId &&
                a.IsSystemAccount == false, ct)
            ?? throw new NotFoundException(
                $"Account not found for user '{query.UserId}'.");

        var accountNumber = account.AccountNumber;

        // ── Fetch all matching transaction legs ───────────────────────────────
        // A leg "belongs" to this user if their account number appears on
        // either side of the transaction — this covers all three leg types:
        //   CustomerTransfer: user appears as Source
        //   NGLDebit:         user appears as Dest
        //   FeeCapture:       neither side is user — excluded correctly
        //
        // We intentionally include FeeCapture legs when grouping by Reference
        // so the caller can see the full ledger for any transfer they initiated.
        // The filter below pulls all legs sharing any Reference that has at
        // least one leg involving the user's account number.

        // Step A — collect all References that involve this user
        var userReferences = await _db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.SourceAccountNumber == accountNumber ||
                t.DestAccountNumber == accountNumber)
            .Select(t => t.Reference)
            .Distinct()
            .ToListAsync(ct);

        if (userReferences.Count == 0)
            return new GetTransactionHistoryResult(
                PageNumber: query.PageNumber,
                PageSize: query.PageSize,
                TotalCount: 0,
                TotalPages: 0,
                Transactions: Array.Empty<TransactionGroupDto>());

        // Step B — count distinct References for pagination metadata
        var totalCount = userReferences.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        // Step C — paginate the References (order by most recent first)
        // We order by the earliest CreatedAt of any leg in that group.
        // To do this in SQL we pull the min CreatedAt per Reference first.
        var pagedReferences = await _db.Transactions
            .AsNoTracking()
            .Where(t => userReferences.Contains(t.Reference))
            .GroupBy(t => t.Reference)
            .Select(g => new
            {
                Reference = g.Key,
                EarliestAt = g.Min(t => t.CreatedAt)
            })
            .OrderByDescending(g => g.EarliestAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(g => g.Reference)
            .ToListAsync(ct);

        if (pagedReferences.Count == 0)
            return new GetTransactionHistoryResult(
                PageNumber: query.PageNumber,
                PageSize: query.PageSize,
                TotalCount: totalCount,
                TotalPages: totalPages,
                Transactions: Array.Empty<TransactionGroupDto>());

        // Step D — fetch all legs for the paged References
        var legs = await _db.Transactions
            .AsNoTracking()
            .Where(t => pagedReferences.Contains(t.Reference))
            .OrderBy(t => t.Reference)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        // Step E — group legs into TransactionGroupDto
        var groups = legs
            .GroupBy(t => t.Reference)
            .Select(g =>
            {
                // The CustomerTransfer leg carries the canonical amounts
                var primary = g.FirstOrDefault(t =>
                    t.Type == Domain.Enums.TransactionType.CustomerTransfer)
                    ?? g.First();

                var legDtos = g
                    .Select(t => new TransactionLegDto(
                        Id: t.Id,
                        Type: t.Type,
                        SourceAccountNumber: t.SourceAccountNumber,
                        DestAccountNumber: t.DestAccountNumber,
                        Amount: t.Amount,
                        Fee: t.Fee,
                        TotalDebited: t.TotalDebited,
                        Narration: t.Narration,
                        Status: t.Status,
                        CreatedAt: t.CreatedAt))
                    .ToList();

                return new TransactionGroupDto(
                    Reference: g.Key,
                    Amount: primary.Amount,
                    Fee: primary.Fee,
                    TotalDebited: primary.TotalDebited,
                    Status: primary.Status,
                    CreatedAt: g.Min(t => t.CreatedAt),
                    Legs: legDtos);
            })
            // Preserve the descending order established by pagedReferences
            .OrderByDescending(g => g.CreatedAt)
            .ToList();

        return new GetTransactionHistoryResult(
            PageNumber: query.PageNumber,
            PageSize: query.PageSize,
            TotalCount: totalCount,
            TotalPages: totalPages,
            Transactions: groups);
    }
}