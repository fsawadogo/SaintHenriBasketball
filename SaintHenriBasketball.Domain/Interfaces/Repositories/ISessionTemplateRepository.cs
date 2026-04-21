using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionTemplateRepository
{
    Task<IReadOnlyList<SessionTemplate>> GetAllAsync();
    Task<SessionTemplate?> GetByIdAsync(Guid id);
    Task AddAsync(SessionTemplate template);
    Task UpdateAsync(SessionTemplate template);
    Task DeleteAsync(Guid id);
}
