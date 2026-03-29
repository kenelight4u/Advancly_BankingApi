using BankingApi.Domain.Enums;
using BankingApi.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Application.Auth.Commands;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    private readonly BankingDbContext _db;

    public RegisterUserCommandValidator(BankingDbContext db)
    {
        _db = db;

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.MiddleName)
            .MaximumLength(100).WithMessage("Middle name must not exceed 100 characters.")
            .When(x => x.MiddleName is not null);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage("Gender is required.")
            .Must(g => g is Gender.Male or Gender.Female or Gender.Other)
            .WithMessage($"Gender must be one of: {Gender.Male}, {Gender.Female}, {Gender.Other}.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.")
            .MustAsync(BeUniqueEmailAsync)
            .WithMessage("An account with this email address already exists.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.BVN)
            .NotEmpty().WithMessage("BVN is required.")
            .Length(11).WithMessage("BVN must be exactly 11 digits.")
            .Matches("^[0-9]{11}$").WithMessage("BVN must contain only numeric digits.")
            .MustAsync(BeUniqueBvnAsync)
            .WithMessage("An account with this BVN already exists.");

        RuleFor(x => x.Address)
            .MaximumLength(255).WithMessage("Address must not exceed 255 characters.")
            .When(x => x.Address is not null);

        RuleFor(x => x.State)
            .MaximumLength(100).WithMessage("State must not exceed 100 characters.")
            .When(x => x.State is not null);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required.")
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters.");
    }

    private async Task<bool> BeUniqueEmailAsync(
        string email, CancellationToken ct) =>
            !await _db.Users.AnyAsync(u => u.Email == email, ct);

    private async Task<bool> BeUniqueBvnAsync(
        string bvn, CancellationToken ct) =>
            !await _db.Accounts.AnyAsync(a => a.BVN == bvn, ct);
}