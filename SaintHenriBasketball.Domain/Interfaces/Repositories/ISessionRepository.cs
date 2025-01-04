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
}


