using FluentValidation;
using SaintHenriBasketball.Application.DTOs;

namespace SaintHenriBasketball.Application.Validators;

public class UpdateSessionDtoValidator : AbstractValidator<UpdateSessionDto>
{
    public UpdateSessionDtoValidator()
    {
        RuleFor(x => x.SessionDate)
            .Must(BeInFuture).WithMessage("Session date must be in the future")
            .When(x => x.SessionDate != default);

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0)
            .LessThanOrEqualTo(30)
            .When(x => x.MaxCapacity != default);

        RuleFor(x => x.DropInPrice)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(10, 2, false)
            .When(x => x.DropInPrice != default);
    }

    private bool BeInFuture(DateTime date)
    {
        return date > DateTime.UtcNow;
    }
}
