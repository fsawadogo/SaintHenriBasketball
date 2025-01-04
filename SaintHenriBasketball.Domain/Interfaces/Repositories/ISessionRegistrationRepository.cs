using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionRegistrationRepository
{
    Task<bool> ExistsAsync(Guid userId, Guid sessionId);
    Task AddAsync(SessionRegistration registration);
    Task DeleteAsync(Guid userId, Guid sessionId);
    Task<IReadOnlyList<SessionRegistration>> GetByUserIdAsync(Guid userId);
}
