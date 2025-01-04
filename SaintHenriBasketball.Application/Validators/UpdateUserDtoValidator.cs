using FluentValidation;
using SaintHenriBasketball.Application.DTOs.Auth;

namespace SaintHenriBasketball.Application.Validators;

public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserDtoValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Username)
            .MinimumLength(3)
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9_]*$").WithMessage("Username can only contain letters, numbers, and underscores")
            .When(x => !string.IsNullOrEmpty(x.Username));

        RuleFor(x => x.FirstName)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.LastName));

        RuleFor(x => x.PaymentPlan)
            .IsInEnum();
    }
}