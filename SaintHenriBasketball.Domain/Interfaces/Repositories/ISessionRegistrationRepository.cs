using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionRegistrationRepository
{
    Task<bool> ExistsAsync(Guid userId, Guid sessionId);
    Task AddAsync(SessionRegistration registration);
    Task DeleteAsync(Guid userId, Guid sessionId);
    Task<IReadOnlyList<SessionRegistration>> GetByUserIdAsync(Guid userId);
    Task<IReadOnlyList<SessionRegistration>> GetByUserIdInRangeAsync(Guid userId, DateTime rangeStart, DateTime rangeEnd);
    /// Every (player, session) registration for sessions dated in the range, in one query.
    Task<IReadOnlyList<(Guid UserId, Guid SessionId)>> GetRegistrationPairsInRangeAsync(DateTime rangeStart, DateTime rangeEnd);
    Task<IReadOnlyList<SessionRegistration>> GetBySessionIdAsync(Guid sessionId);
    Task<int> GetRegistrationCountForSessionAsync(Guid sessionId);
    Task<bool> IsUserRegisteredAsync(Guid userId, Guid sessionId);
}