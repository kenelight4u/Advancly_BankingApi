using FluentValidation;

namespace BankingApi.Application.Transactions.Commands;

public class TransferFundsCommandValidator : AbstractValidator<TransferFundsCommand>
{
    public TransferFundsCommandValidator()
    {
        // SenderId injected from JWT — must always be present
        RuleFor(x => x.SenderId)
            .NotEmpty().WithMessage("SenderId is required.");

        // Destination account number — exactly 10 numeric digits
        RuleFor(x => x.DestAccountNumber)
            .NotEmpty().WithMessage("Destination account number is required.")
            .Length(10).WithMessage("Destination account number must be exactly 10 digits.")
            .Matches("^[0-9]{10}$")
            .WithMessage("Destination account number must contain only numeric digits.");

        // Amount must be a positive value
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Transfer amount must be greater than zero.");

        // Narration is optional but bounded
        RuleFor(x => x.Narration)
            .MaximumLength(255).WithMessage("Narration must not exceed 255 characters.")
            .When(x => x.Narration is not null);
    }
}