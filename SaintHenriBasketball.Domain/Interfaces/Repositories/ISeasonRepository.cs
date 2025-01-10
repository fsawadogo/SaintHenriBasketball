using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISeasonRepository
{
    Task<Season?> GetByIdAsync(Guid id);
    Task<Season?> GetByIdWithRegistrationsAsync(Guid id);
    Task<List<Season>> GetAllAsync();
    Task<List<Season>> GetAllWithRegistrationsAsync();
    Task<bool> ExistsAsync(Guid id);
    Task<Season> AddAsync(Season season);
    Task UpdateAsync(Season season);
    Task DeleteAsync(Season season);
    Task<bool> HasUserRegisteredAsync(Guid seasonId, Guid userId);
    Task<SeasonRegistration?> GetRegistrationAsync(Guid seasonId, Guid userId);
    Task DeleteRegistrationAsync(SeasonRegistration registration);
    Task<List<Season>> GetActiveSeasonsByDateRangeAsync(DateTime startDate, DateTime endDate);
}