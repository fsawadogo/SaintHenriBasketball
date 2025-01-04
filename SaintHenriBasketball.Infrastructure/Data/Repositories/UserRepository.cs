using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationUser> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.SessionRegistrations)
                .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<ApplicationUser> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.SessionRegistrations)
                .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<ApplicationUser> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .Include(u => u.SessionRegistrations)
                .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await _context.Users
            .AnyAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
        return await _context.Users
            .Include(u => u.SessionRegistrations)
                .ThenInclude(sr => sr.Session)
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task AddAsync(ApplicationUser user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ApplicationUser user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
}