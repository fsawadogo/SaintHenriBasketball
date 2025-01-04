using SaintHenriBasketball.Application.DTOs;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IRegistrationService
{
    Task<SessionRegistrationResponseDto> RegisterUserForSessionAsync(Guid userId, Guid sessionId);
    Task CancelRegistrationAsync(Guid userId, Guid sessionId);
    Task<IReadOnlyList<SessionRegistrationResponseDto>> GetUserRegistrationsAsync(Guid userId);
}