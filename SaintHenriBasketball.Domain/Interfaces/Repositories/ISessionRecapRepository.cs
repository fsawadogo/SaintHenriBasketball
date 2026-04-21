using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionRecapRepository
{
    Task AddAsync(SessionRecap recap);
    Task DeleteAsync(Guid recapId);
    Task<SessionRecap?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<SessionRecap>> GetBySessionAsync(Guid sessionId);
    Task<IReadOnlyList<SessionRecap>> GetRecentAsync(int take);
}
