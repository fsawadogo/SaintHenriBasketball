using FluentValidation;
using SaintHenriBasketball.Application.DTOs.Attendance;

namespace SaintHenriBasketball.Application.Validators;

public class RemoveParticipantsRequestValidator : AbstractValidator<RemoveParticipantsRequest>
{
    public RemoveParticipantsRequestValidator()
    {
        RuleFor(x => x.UserIds)
            .NotEmpty()
            .WithMessage("At least one user ID must be provided")
            .Must(userIds => userIds.Count <= 50)
            .WithMessage("Cannot remove more than 50 participants at once");

        RuleForEach(x => x.UserIds)
            .NotEmpty()
            .WithMessage("User ID cannot be empty");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Reason cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Reason));

        RuleFor(x => x.AdminNotes)
            .MaximumLength(500)
            .WithMessage("Admin notes cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.AdminNotes));
    }
}
