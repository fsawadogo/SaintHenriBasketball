using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionRepository
{
    Task<Session> GetByIdAsync(Guid id);
    Task AddAsync(Session session);
    Task UpdateAsync(Session session);
    Task<IReadOnlyList<Session>> GetUpcomingSessionsAsync();
    Task<IReadOnlyList<Session>> GetAvailableSessionsAsync();
    Task<IEnumerable<Session>> GetUserSessionsAsync(Guid userId);
    Task<IReadOnlyList<Session>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<int> GetRegistrationCountAsync(Guid sessionId);
    Task<bool> ExistsAsync(Guid id);
    Task<Session> GetClosestSessionAsync();
    Task<Session> GetNextSessionAsync();
    Task<IReadOnlyList<Session>> GetAllSessionsAsync();
}


