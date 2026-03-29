using BankingApi.Domain.Enums;
using FluentValidation;

namespace BankingApi.Application.Accounts.Commands;

public class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        // UserId must always be present — sourced from JWT
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        // All profile fields are optional (PATCH semantics)
        // but when provided they must meet length/format constraints

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name cannot be empty when provided.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.")
            .When(x => x.FirstName is not null);

        RuleFor(x => x.MiddleName)
            .MaximumLength(100).WithMessage("Middle name must not exceed 100 characters.")
            .When(x => x.MiddleName is not null);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name cannot be empty when provided.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.")
            .When(x => x.LastName is not null);

        RuleFor(x => x.Gender)
            .Must(g => g is Gender.Male or Gender.Female or Gender.Other)
            .WithMessage($"Gender must be one of: {Gender.Male}, {Gender.Female}, {Gender.Other}.")
            .When(x => x.Gender is not null);

        RuleFor(x => x.Address)
            .MaximumLength(255).WithMessage("Address must not exceed 255 characters.")
            .When(x => x.Address is not null);

        RuleFor(x => x.State)
            .MaximumLength(100).WithMessage("State must not exceed 100 characters.")
            .When(x => x.State is not null);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country cannot be empty when provided.")
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters.")
            .When(x => x.Country is not null);
    }
}