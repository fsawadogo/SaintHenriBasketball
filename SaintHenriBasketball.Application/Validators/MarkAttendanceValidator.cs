using FluentValidation;

namespace SaintHenriBasketball.Application.Validators;

public class MarkAttendanceValidator : AbstractValidator<(Guid sessionId, Guid userId, bool isPresent, string? notes)>
{
    public MarkAttendanceValidator()
    {
        RuleFor(x => x.sessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.userId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.notes)
            .MaximumLength(500).When(x => x.notes != null)
            .WithMessage("Notes cannot exceed 500 characters");
    }
}