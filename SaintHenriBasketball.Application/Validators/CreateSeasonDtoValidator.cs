using FluentValidation;
using SaintHenriBasketball.Application.DTOs.Season;
namespace SaintHenriBasketball.Application.Validators;

public class CreateSeasonDtoValidator : AbstractValidator<CreateSeasonDto>
{
    public CreateSeasonDtoValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required")
            .Must(BeInFuture).WithMessage("Start date must be in the future");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .Must(BeInFuture).WithMessage("End date must be in the future")
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");

        RuleFor(x => x.Price)
            .NotEmpty().WithMessage("Price is required")
            .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0")
            .PrecisionScale(10, 2, false).WithMessage("Price cannot have more than 2 decimal places");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }

    private bool BeInFuture(DateTime date)
    {
        return date > DateTime.UtcNow;
    }
}