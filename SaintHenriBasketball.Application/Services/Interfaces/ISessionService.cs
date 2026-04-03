using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISessionService
{
    Task<SessionDto> CreateSessionAsync(CreateSessionDto createSessionDto);
    Task<IReadOnlyList<SessionDto>> GetAllSessionsAsync();
    Task<SessionDetailDto> GetSessionAsync(Guid id);
    Task<IReadOnlyList<SessionDto>> GetUpcomingSessionsAsync();
    Task UpdateSessionAsync(Guid id, UpdateSessionDto updateSessionDto);
    Task CancelSessionAsync(Guid id);
    Task<IReadOnlyList<SessionDto>> GetAvailableSessionsAsync();
    Task<SessionRegistrationResponseDto> RegisterForSessionAsync(Guid sessionId, Guid userId);
    Task UnregisterFromSessionAsync(Guid sessionId, Guid userId);
    Task<IReadOnlyList<SessionDto>> GetUserSessionsAsync(Guid userId);
    Task<bool> IsUserRegisteredForSessionAsync(Guid sessionId, Guid userId);
    
    // Admin methods for participant management
    Task<SessionRegistrationResponseDto> AddParticipantToSessionAsync(Guid sessionId, Guid userId);
    Task RemoveParticipantFromSessionAsync(Guid sessionId, Guid userId);

    // Bulk session generation
    Task<GenerateSessionsResponseDto> GenerateSessionsForSeasonAsync(GenerateSessionsDto request);
}
