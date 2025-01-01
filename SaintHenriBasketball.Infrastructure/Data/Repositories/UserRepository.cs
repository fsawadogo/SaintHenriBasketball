using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class UserRepository : GenericRepository<ApplicationUser>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<ApplicationUser> GetByEmailAsync(string email)
    {
        return await _context.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<ApplicationUser> GetByUsernameAsync(string username)
    {
        return await _context.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Set<ApplicationUser>()
            .AnyAsync(u => u.Email == email);
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await _context.Set<ApplicationUser>()
            .AnyAsync(u => u.Username == username);
    }
    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
        return await _context.Set<ApplicationUser>().ToListAsync();
     }
}
