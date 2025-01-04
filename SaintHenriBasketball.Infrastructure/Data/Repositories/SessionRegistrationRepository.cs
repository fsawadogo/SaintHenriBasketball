using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SessionRegistrationRepository : ISessionRegistrationRepository
{
    private readonly ApplicationDbContext _context;

    public SessionRegistrationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid sessionId)
    {
        return await _context.SessionRegistrations
            .AnyAsync(r => r.UserId == userId && r.SessionId == sessionId);
    }

    public async Task AddAsync(SessionRegistration registration)
    {
        await _context.SessionRegistrations.AddAsync(registration);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid userId, Guid sessionId)
    {
        var registration = await _context.SessionRegistrations
            .FirstOrDefaultAsync(r => r.UserId == userId && r.SessionId == sessionId);

        if (registration != null)
        {
            _context.SessionRegistrations.Remove(registration);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<SessionRegistration>> GetByUserIdAsync(Guid userId)
    {
        return await _context.SessionRegistrations
            .Include(r => r.Session)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Session.SessionDate)
            .ToListAsync();
    }
}