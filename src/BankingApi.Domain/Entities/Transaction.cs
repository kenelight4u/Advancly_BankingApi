namespace BankingApi.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string SourceAccountNumber { get; set; } = string.Empty;
    public string DestAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal TotalDebited { get; set; }
    public string? Narration { get; set; }
    public string Status { get; set; } = string.Empty; 
    public string Type { get; set; } = string.Empty;           
    public DateTime CreatedAt { get; set; }
}