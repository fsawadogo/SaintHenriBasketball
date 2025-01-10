using FluentValidation;
using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.Application.Validators;

public class SessionRegistrationDtoValidator : AbstractValidator<SessionRegistrationDto>
{
    public SessionRegistrationDtoValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();
    }
}