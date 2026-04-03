using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IWaitlistRepository
{
    Task<IReadOnlyList<Waitlist>> GetBySessionAsync(Guid sessionId);
    Task<Waitlist?> GetByUserAndSessionAsync(Guid userId, Guid sessionId);
    Task<Waitlist?> GetNextInLineAsync(Guid sessionId);
    Task<int> GetWaitlistCountAsync(Guid sessionId);
    Task AddAsync(Waitlist entry);
    Task UpdateAsync(Waitlist entry);
    Task DeleteAsync(Guid id);
}
