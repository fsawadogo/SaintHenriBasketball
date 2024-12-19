using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionRepository : IGenericRepository<TrainingSession>
{
    Task<TrainingSession> GetSessionWithRegistrationsAsync(Guid id);
    Task<IReadOnlyList<TrainingSession>> GetUpcomingSessionsAsync();
    Task<bool> IsSessionAvailableAsync(Guid sessionId);
}
