using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISessionService
{
    Task<SessionDto> CreateSessionAsync(CreateSessionDto createSessionDto);
    Task<SessionDetailDto> GetSessionAsync(Guid id);
    Task<IReadOnlyList<SessionDto>> GetUpcomingSessionsAsync();
    Task UpdateSessionAsync(Guid id, UpdateSessionDto updateSessionDto);
    Task CancelSessionAsync(Guid id);
    Task<IReadOnlyList<SessionDto>> GetAvailableSessionsAsync();
    Task<SessionRegistrationResponseDto> RegisterForSessionAsync(Guid sessionId, Guid userId);
    Task UnregisterFromSessionAsync(Guid sessionId, Guid userId);
    Task<IReadOnlyList<SessionDto>> GetUserSessionsAsync(Guid userId);
    Task<bool> IsUserRegisteredForSessionAsync(Guid sessionId, Guid userId);
}
