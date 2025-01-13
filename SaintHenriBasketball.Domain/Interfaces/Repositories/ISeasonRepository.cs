using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISeasonRepository
{
    Task<Season?> GetByIdAsync(Guid id);
    Task<Season?> GetByIdWithRegistrationsAsync(Guid id);
    Task<List<Season>> GetAllAsync();
    Task<List<Season>> GetAllWithRegistrationsAsync();
    Task<Season?> GetCurrentSeasonAsync();
    Task<bool> HasUserRegisteredAsync(Guid seasonId, Guid userId);
    Task<SeasonRegistration?> GetRegistrationAsync(Guid seasonId, Guid userId);
    Task<Season> AddAsync(Season season);
    Task UpdateAsync(Season season);
    Task DeleteAsync(Season season);
    Task DeleteRegistrationAsync(SeasonRegistration registration);
    Task AddRegistrationAsync(SeasonRegistration registration); // Add this new method
}