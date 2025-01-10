using System.Text.RegularExpressions;
using FluentValidation;
using SaintHenriBasketball.Application.DTOs;

namespace SaintHenriBasketball.Application.Validators;

public class CreateSessionDtoValidator : AbstractValidator<CreateSessionDto>
{
    private static readonly Regex TimeFormatRegex = new Regex(@"^([0-1][0-9]|2[0-3]):[0-5][0-9]$", RegexOptions.Compiled);

    public CreateSessionDtoValidator()
    {
        RuleFor(x => x.SessionDate)
            .NotEmpty().WithMessage("Session date is required")
            .Must(BeInFuture).WithMessage("Session date must be in the future");

        RuleFor(x => x.MaxCapacity)
            .NotEmpty().WithMessage("Maximum capacity is required")
            .GreaterThan(0).WithMessage("Maximum capacity must be greater than 0")
            .LessThanOrEqualTo(30).WithMessage("Maximum capacity cannot exceed 30");

        RuleFor(x => x.DropInPrice)
            .NotEmpty().WithMessage("Drop-in price is required")
            .GreaterThanOrEqualTo(0).WithMessage("Drop-in price must be greater than or equal to 0")
            .PrecisionScale(10, 2, false).WithMessage("Drop-in price cannot have more than 2 decimal places");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("Start time is required")
            .Must(BeValidTimeFormat).WithMessage("Start time must be in 24-hour format (HH:mm)");

        RuleFor(x => x.EndTime)
            .NotEmpty().WithMessage("End time is required")
            .Must(BeValidTimeFormat).WithMessage("End time must be in 24-hour format (HH:mm)");

        RuleFor(x => new { x.StartTime, x.EndTime })
            .Must(x => BeValidTimeRange(x.StartTime, x.EndTime))
            .When(x => BeValidTimeFormat(x.StartTime) && BeValidTimeFormat(x.EndTime))
            .WithMessage("End time must be after start time");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location is required")
            .MaximumLength(100).WithMessage("Location cannot exceed 100 characters");
    }

    private bool BeInFuture(DateTime date)
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