using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(ApplicationDbContext context, ILogger<UserRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApplicationUser> GetByIdAsync(Guid id)
    {
        return (await _context.Users
            .Include(u => u.SessionRegistrations)
            .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(u => u.Id == id))!;
    }

    public async Task<ApplicationUser> GetByEmailAsync(string? email)
    {
        return (await _context.Users
            .Include(u => u.SessionRegistrations)
            .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(u => u.Email != null && email != null && u.Email.ToLower() == email.ToLower()))!;
    }

    public async Task<ApplicationUser> GetByUsernameAsync(string username)
    {
        return (await _context.Users
            .Include(u => u.SessionRegistrations)
            .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == username.ToLower()))!;
    }

    public async Task<bool> EmailExistsAsync(string? email)
    {
        return await _context.Users
            .AnyAsync(u => u.Email != null && email != null && u.Email.ToLower() == email.ToLower());
    }

    public async Task<bool> UsernameExistsAsync(string? username)
    {
        return await _context.Users
            .AnyAsync(u => u.Username != null && username != null && u.Username.ToLower() == username.ToLower());
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
    
    public async Task DeleteAsync(ApplicationUser user)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ApplicationUser>> GetUsersByIdsAsync(List<Guid> userIds)
    {
        try
        {
            if (!userIds.Any())
            {
                return new List<ApplicationUser>();
            }

            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} users from {Total} requested IDs",
                users.Count, userIds.Count);

            if (users.Count < userIds.Count)
            {
                var missingIds = userIds.Except(users.Select(u => u.Id));
                _logger.LogWarning("Some requested user IDs were not found: {MissingIds}",
                    string.Join(", ", missingIds));
            }

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users by IDs. Requested IDs: {UserIds}",
                string.Join(", ", userIds));
            throw;
        }
    }
}