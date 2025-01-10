using FluentValidation;
using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.Application.Validators;

public class CreateSessionDtoValidator : AbstractValidator<CreateSessionDto>
{
    public CreateSessionDtoValidator()
    {
        RuleFor(x => x.SessionDate)
            .NotEmpty()
            .Must(BeInFuture).WithMessage("Session date must be in the future");

        RuleFor(x => x.MaxCapacity)
            .NotEmpty()
            .GreaterThan(0)
            .LessThanOrEqualTo(20);

        RuleFor(x => x.DropInPrice)
            .NotEmpty()
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(10, 2, false);
    }

    private bool BeInFuture(DateTime date)
    {
        return date > DateTime.UtcNow;
    }
}
