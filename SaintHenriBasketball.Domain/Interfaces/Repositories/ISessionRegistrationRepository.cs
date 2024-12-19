using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionRegistrationRepository : IGenericRepository<SessionRegistration>
{
    Task<SessionRegistration> GetRegistrationByPlayerAndSessionAsync(Guid playerId, Guid sessionId);
    Task<IReadOnlyList<SessionRegistration>> GetRegistrationsByPlayerAsync(Guid playerId);
}
