using SaintHenriBasketball.Application.DTOs.SessionRecap;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISessionRecapService
{
    Task<SessionRecapDto> CreateAsync(Guid sessionId, string photoUrl, string? caption, Guid adminId);
    Task DeleteAsync(Guid recapId);
    Task<IReadOnlyList<SessionRecapDto>> GetBySessionAsync(Guid sessionId);
    Task<IReadOnlyList<SessionRecapDto>> GetRecentAsync(int take = 6);
}
