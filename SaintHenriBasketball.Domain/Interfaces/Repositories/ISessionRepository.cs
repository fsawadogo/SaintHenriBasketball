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
    Task<int> CountSessionsBetweenAsync(DateTime from, DateTime to);

    /// Counts of the rows deleting the session would remove, or null when it doesn't exist.
    Task<SessionDeletionImpact?> GetDeletionImpactAsync(Guid sessionId);

    /// Deletes the session with its registrations, attendance, waitlist, feedback and recaps in one transaction.
    /// Returns false, deleting nothing, when the session is gone or has payments.
    Task<bool> DeleteWithDependentsAsync(Guid sessionId);
}


