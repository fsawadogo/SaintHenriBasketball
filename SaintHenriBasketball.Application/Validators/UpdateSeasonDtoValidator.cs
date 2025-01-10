using FluentValidation;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.DTOs.Season;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace SaintHenriBasketball.Application.Validators;

public class UpdateSeasonDtoValidator : AbstractValidator<UpdateSeasonDto>
{
    public UpdateSeasonDtoValidator()
    {
        When(x => x.StartDate.HasValue, () =>
        {
            RuleFor(x => x.StartDate)
                .Must(BeInFuture).WithMessage("Start date must be in the future");
        });

        When(x => x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate)
                .Must(BeInFuture).WithMessage("End date must be in the future");
        });

        When(x => x.StartDate.HasValue && x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate)
                .GreaterThan(x => x.StartDate!.Value)
                .WithMessage("End date must be after start date");
        });

        When(x => x.Price.HasValue, () =>
        {
            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0")
                .PrecisionScale(10, 2, false).WithMessage("Price cannot have more than 2 decimal places");
        });

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters")
            .When(x => x.Notes != null);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid season status")
            .When(x => x.Status.HasValue);
    }

    private bool BeInFuture(DateTime? date)
    {
        return date > DateTime.UtcNow;
    }
}