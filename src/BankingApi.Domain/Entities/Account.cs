namespace BankingApi.Domain.Entities;

public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? BVN { get; set; }                      // null for NGL system accounts
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NGN";
    public string AccountType { get; set; } = "Customer"; // Customer | NGL
    public string? NglPoolType { get; set; }              // Credit | Debit | Fee | null
    public bool IsSystemAccount { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}