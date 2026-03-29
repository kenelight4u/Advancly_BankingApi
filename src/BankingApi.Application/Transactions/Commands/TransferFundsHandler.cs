using BankingApi.Application.Common.Exceptions;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Infrastructure.Persistence;
using BankingApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Application.Transactions.Commands;

/// <summary>
/// Handles TransferFundsCommand.
///
/// Implements the two-NGL-account ledger pattern:
///   LEG 1 — Sender        → NGL Credit  (Amount + Fee)   [CustomerTransfer]
///   LEG 2 — NGL Credit    → NGL Fee   (Fee only)       [FeeCapture]
///   LEG 3 — NGL Debit     → Recipient   (Amount only)    [NGLDebit]
///
/// All five balance mutations and three transaction inserts execute inside
/// a single EF Core database transaction. Any failure rolls everything back.
/// </summary>
public class TransferFundsHandler
{
    private readonly BankingDbContext _db;
    private readonly IFeeCalculator _feeCalculator;

    public TransferFundsHandler(
        BankingDbContext db,
        IFeeCalculator feeCalculator)
    {
        _db = db;
        _feeCalculator = feeCalculator;
    }

    public async Task<TransferFundsResult> Handle(
        TransferFundsCommand cmd,
        CancellationToken ct)
    {
        // ── Open explicit DB transaction ──────────────────────────────────────
        await using var dbTransaction =
            await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // ── STEP 1: Load sender (must be a Customer account owned by JWT user) ──
            var senderAccount = await _db.Accounts
                .FirstOrDefaultAsync(a =>
                    a.UserId == cmd.SenderId &&
                    a.AccountType == AccountType.Customer, ct)
                ?? throw new NotFoundException(
                    "Sender account not found.");

            // ── STEP 2: Load recipient (must be a Customer account) ───────────
            var recipientAccount = await _db.Accounts
                .FirstOrDefaultAsync(a =>
                    a.AccountNumber == cmd.DestAccountNumber &&
                    a.AccountType == AccountType.Customer, ct)
                ?? throw new NotFoundException(
                    $"Recipient account '{cmd.DestAccountNumber}' not found.");

            // ── STEP 3: Business rule — cannot transfer to self ───────────────
            if (senderAccount.AccountNumber == cmd.DestAccountNumber)
                throw new ValidationException(
                    "Cannot transfer funds to the same account.");

            // ── STEP 4: Compute fee and total debit ───────────────────────────
            var fee = Math.Round(_feeCalculator.Calculate(cmd.Amount), 2);
            var totalDebited = Math.Round(cmd.Amount + fee, 2);
            var amount = Math.Round(cmd.Amount, 2);

            // ── STEP 5: Validate sufficient balance ───────────────────────────
            if (senderAccount.Balance < totalDebited)
                throw new InsufficientFundsException(
                    available: senderAccount.Balance,
                    required: totalDebited);

            // ── STEP 6: Load NGL accounts ─────────────────────────────────────
            var nglCredit = await _db.Accounts
                .FirstOrDefaultAsync(a =>
                    a.AccountType == AccountType.NGL &&
                    a.NglPoolType == NglPoolType.Credit, ct)
                ?? throw new NotFoundException(
                    "NGL Credit account is not configured. " +
                    "Ensure the database has been seeded correctly.");

            var nglDebit = await _db.Accounts
                .FirstOrDefaultAsync(a =>
                    a.AccountType == AccountType.NGL &&
                    a.NglPoolType == NglPoolType.Debit, ct)
                ?? throw new NotFoundException(
                    "NGL Debit account is not configured. " +
                    "Ensure the database has been seeded correctly.");

            var nglFee = await _db.Accounts
                .FirstOrDefaultAsync(a =>
                    a.AccountType == AccountType.NGL &&
                    a.NglPoolType == NglPoolType.Fee, ct)
                ?? throw new NotFoundException(
                    "NGL Fee account is not configured. " +
                    "Ensure the database has been seeded correctly.");

            var reference = GenerateReference();
            var now = DateTime.UtcNow;

            // ── LEG 1: Sender → NGL Credit (Amount + Fee) ────────────────────
            senderAccount.Balance -= totalDebited;
            nglCredit.Balance += totalDebited;
            senderAccount.UpdatedAt = now;
            nglCredit.UpdatedAt = now;

            _db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                SourceAccountNumber = senderAccount.AccountNumber,
                DestAccountNumber = nglCredit.AccountNumber,
                Amount = amount,
                Fee = fee,
                TotalDebited = totalDebited,
                Narration = cmd.Narration,
                Type = TransactionType.CustomerTransfer,
                Status = TransactionStatus.Completed,
                CreatedAt = now
            });

            // ── LEG 2: NGL Credit → NGL Debit (Fee settlement) ───────────────
            nglCredit.Balance -= fee;
            nglFee.Balance += fee;
            nglCredit.UpdatedAt = now;
            nglFee.UpdatedAt = now;

            _db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                SourceAccountNumber = nglCredit.AccountNumber,
                DestAccountNumber = nglFee.AccountNumber,
                Amount = fee,
                Fee = 0.00m,
                TotalDebited = fee,
                Narration = "Fee settlement",
                Type = TransactionType.FeeCapture,
                Status = TransactionStatus.Completed,
                CreatedAt = now
            });

            // ── LEG 3: NGL Debit → Recipient (Amount only) ───────────────────
            nglDebit.Balance -= amount;
            recipientAccount.Balance += amount;
            nglDebit.UpdatedAt = now;
            recipientAccount.UpdatedAt = now;

            _db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                SourceAccountNumber = nglDebit.AccountNumber,
                DestAccountNumber = recipientAccount.AccountNumber,
                Amount = amount,
                Fee = 0.00m,
                TotalDebited = amount,
                Narration = cmd.Narration,
                Type = TransactionType.NGLDebit,
                Status = TransactionStatus.Completed,
                CreatedAt = now
            });

            // ── Persist all mutations atomically ──────────────────────────────
            await _db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            return new TransferFundsResult(
                Reference: reference,
                Amount: amount,
                Fee: fee,
                TotalDebited: totalDebited,
                RecipientAccountNumber: recipientAccount.AccountNumber,
                SenderAccountNumber: senderAccount.AccountNumber,
                Status: TransactionStatus.Completed,
                TransactedAt: now);
        }
        catch
        {
            // Roll back all five balance mutations and three inserts
            await dbTransaction.RollbackAsync(ct);
            throw;
        }
    }

    // ── Reference generator ───────────────────────────────────────────────────

    /// <summary>
    /// Produces a collision-resistant reference shared across all three legs.
    /// Format: TXN{yyyyMMddHHmmss}{6-char GUID suffix}
    /// Example: TXN202407011530224F9A1B
    /// </summary>
    private static string GenerateReference() =>
        $"TXN{DateTime.UtcNow:yyyyMMddHHmmss}" +
        $"{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}