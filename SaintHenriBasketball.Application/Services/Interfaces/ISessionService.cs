using SaintHenriBasketball.Application.DTOs;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISessionService
{
    Task<SessionDto> CreateSessionAsync(CreateSessionDto createSessionDto);
    Task<SessionDetailDto> GetSessionAsync(Guid id);
    Task<IReadOnlyList<SessionDto>> GetUpcomingSessionsAsync();
    Task UpdateSessionAsync(Guid id, UpdateSessionDto updateSessionDto);
    Task CancelSessionAsync(Guid id);
    Task<IReadOnlyList<SessionDto>> GetAvailableSessionsAsync();
}