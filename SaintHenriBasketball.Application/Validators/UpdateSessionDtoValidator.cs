using FluentValidation;
using SaintHenriBasketball.Application.DTOs.Session;
using System.Text.RegularExpressions;

namespace SaintHenriBasketball.Application.Validators;

public class UpdateSessionDtoValidator : AbstractValidator<UpdateSessionDto>
{
    private static readonly Regex TimeFormatRegex = new Regex(@"^([0-1][0-9]|2[0-3]):[0-5][0-9]$", RegexOptions.Compiled);

    public UpdateSessionDtoValidator()
    {
        RuleFor(x => x.SessionDate)
            .Must(BeInFuture).WithMessage("Session date must be in the future")
            .When(x => x.SessionDate.HasValue);

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).WithMessage("Maximum capacity must be greater than 0")
            .LessThanOrEqualTo(30).WithMessage("Maximum capacity cannot exceed 30")
            .When(x => x.MaxCapacity.HasValue);

        RuleFor(x => x.DropInPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Drop-in price must be greater than or equal to 0")
            .PrecisionScale(10, 2, false).WithMessage("Drop-in price cannot have more than 2 decimal places")
            .When(x => x.DropInPrice.HasValue);

        RuleFor(x => x.StartTime)
            .Must(BeValidTimeFormat).WithMessage("Start time must be in 24-hour format (HH:mm)")
            .When(x => !string.IsNullOrEmpty(x.StartTime));

        RuleFor(x => x.EndTime)
            .Must(BeValidTimeFormat).WithMessage("End time must be in 24-hour format (HH:mm)")
            .When(x => !string.IsNullOrEmpty(x.EndTime));

        When(x => !string.IsNullOrEmpty(x.StartTime) && !string.IsNullOrEmpty(x.EndTime), () =>
        {
            RuleFor(x => new { x.StartTime, x.EndTime })
                .Must(x => BeValidTimeRange(x.StartTime, x.EndTime))
                .WithMessage("End time must be after start time");
        });

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location is required")
            .MaximumLength(100).WithMessage("Location cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Location));

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid session status");
    }

    private bool BeInFuture(DateTime? date)
    {
        return date > DateTime.UtcNow;
    }

    private bool BeValidTimeFormat(string time)
    {
        return TimeFormatRegex.IsMatch(time);
    }

    private bool BeValidTimeRange(string startTime, string endTime)
    {
        if (TimeSpan.TryParse(startTime, out TimeSpan start) && 
            TimeSpan.TryParse(endTime, out TimeSpan end))
        {
            return end > start;
        }
        return false;
    }
}