using SaintHenriBasketball.Application.DTOs;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IRegistrationService
{
    Task<SessionRegistrationDto> RegisterPlayerForSessionAsync(Guid playerId, Guid sessionId);
    Task CancelRegistrationAsync(Guid playerId, Guid sessionId);
    Task<IReadOnlyList<SessionRegistrationDto>> GetPlayerRegistrationsAsync(Guid playerId);
}