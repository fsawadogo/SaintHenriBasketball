using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<ApplicationUser> GetByIdAsync(Guid id);
    Task<List<ApplicationUser>> GetUsersByIdsAsync(List<Guid> userIds);
    Task<ApplicationUser> GetByEmailAsync(string? email);
    Task<ApplicationUser> GetByUsernameAsync(string username);
    Task<bool> EmailExistsAsync(string? email);
    Task<bool> UsernameExistsAsync(string? username);
    Task<IEnumerable<ApplicationUser>> GetAllUsersAsync();
    Task AddAsync(ApplicationUser user);
    Task UpdateAsync(ApplicationUser user);
    Task DeleteAsync(ApplicationUser user);

}