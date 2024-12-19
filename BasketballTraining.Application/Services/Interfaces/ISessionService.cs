using SaintHenriBasketball.Application.DTOs;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISessionService
{
    Task<SessionDto> CreateSessionAsync(CreateSessionDto createSessionDto);
    Task<SessionDto> GetSessionAsync(Guid id);
    Task<IReadOnlyList<SessionDto>> GetUpcomingSessionsAsync();
    Task UpdateSessionAsync(Guid id, UpdateSessionDto updateSessionDto);
    Task CancelSessionAsync(Guid id);
}