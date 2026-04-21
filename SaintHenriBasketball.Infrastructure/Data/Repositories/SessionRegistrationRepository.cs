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

    public async Task<IReadOnlyList<SessionRegistration>> GetByUserIdInRangeAsync(Guid userId, DateTime rangeStart, DateTime rangeEnd)
    {
        return await _context.SessionRegistrations
            .Include(r => r.Session)
            .Where(r => r.UserId == userId
                        && r.Session.SessionDate >= rangeStart.Date
                        && r.Session.SessionDate <= rangeEnd.Date)
            .OrderBy(r => r.Session.SessionDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SessionRegistration>> GetBySessionIdAsync(Guid sessionId)
    {
        return await _context.SessionRegistrations
            .Include(r => r.User)
            .Where(r => r.SessionId == sessionId)
            .OrderBy(r => r.RegistrationDate)
            .ToListAsync();
    }

    public async Task<int> GetRegistrationCountForSessionAsync(Guid sessionId)
    {
        return await _context.SessionRegistrations
            .CountAsync(r => r.SessionId == sessionId);
    }

    public async Task<bool> IsUserRegisteredAsync(Guid userId, Guid sessionId)
    {
        return await ExistsAsync(userId, sessionId);
    }
}