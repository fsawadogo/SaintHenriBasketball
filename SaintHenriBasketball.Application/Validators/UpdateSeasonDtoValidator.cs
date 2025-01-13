using FluentValidation;
using SaintHenriBasketball.Application.DTOs.Season;

namespace SaintHenriBasketball.Application.Validators;

public class UpdateSeasonDtoValidator : AbstractValidator<UpdateSeasonDto>
{
    public UpdateSeasonDtoValidator()
    {
        When(x => x.StartDate.HasValue, () =>
        {
            RuleFor(x => x.StartDate)
                .Must(BeInFuture).WithMessage("Start date must be in the future")
                .Must((dto, startDate) => !dto.EndDate.HasValue || startDate < dto.EndDate)
                .WithMessage("Start date must be before end date");
        });

        When(x => x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate)
                .Must(BeInFuture).WithMessage("End date must be in the future")
                .Must((dto, endDate) => !dto.StartDate.HasValue || endDate > dto.StartDate)
                .WithMessage("End date must be after start date");
        });

        When(x => x.Price.HasValue, () =>
        {
            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0")
                .LessThanOrEqualTo(1000).WithMessage("Price cannot exceed $1,000")
                .PrecisionScale(10, 2, false).WithMessage("Price cannot have more than 2 decimal places");
        });

        When(x => x.Notes != null, () =>
        {
            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters")
                .Must(notes => !string.IsNullOrWhiteSpace(notes))
                .WithMessage("Notes cannot be empty or whitespace");
        });

        // At least one property must be set for update
        RuleFor(x => x)
            .Must(HaveAtLeastOnePropertySet)
            .WithMessage("At least one property must be set for update");
    }

    private bool BeInFuture(DateTime? date)
    {
        if (!date.HasValue) return true;
        return date.Value.Date >= DateTime.UtcNow.Date;
    }

    private bool HaveAtLeastOnePropertySet(UpdateSeasonDto dto)
    {
        return dto.StartDate.HasValue ||
               dto.EndDate.HasValue ||
               dto.Price.HasValue ||
               dto.Notes != null;
    }
}