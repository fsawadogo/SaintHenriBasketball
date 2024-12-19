using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<ApplicationUser>
{
    Task<ApplicationUser> GetByEmailAsync(string email);
    Task<ApplicationUser> GetByUsernameAsync(string username);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> UsernameExistsAsync(string username);
}