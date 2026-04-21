using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionFeedbackRepository
{
    Task AddAsync(SessionFeedback feedback);
    Task<bool> ExistsAsync(Guid sessionId, Guid userId);
    Task<IReadOnlyList<SessionFeedback>> GetBySessionAsync(Guid sessionId);
}
