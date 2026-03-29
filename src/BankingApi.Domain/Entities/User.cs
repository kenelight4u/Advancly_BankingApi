namespace BankingApi.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;   // Male | Female | Other | System
    public string? Address { get; set; }
    public string? State { get; set; }
    public string Country { get; set; } = "Nigeria";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // BCrypt hash
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Account? Account { get; set; }

    // Computed — never persisted
    public string FullName =>
        string.Join(" ", new[] { FirstName, MiddleName, LastName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}