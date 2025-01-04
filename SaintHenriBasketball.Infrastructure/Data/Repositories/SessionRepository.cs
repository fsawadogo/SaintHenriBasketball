using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly ApplicationDbContext _context;

    public SessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Session> GetByIdAsync(Guid id)
    {
        return await _context.Sessions
            .Include(s => s.Registrations)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task AddAsync(Session session)
    {
        await _context.Sessions.AddAsync(session);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Session session)
    {
        _context.Sessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Session>> GetUpcomingSessionsAsync()
    {
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s => s.SessionDate > DateTime.UtcNow)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Session>> GetAvailableSessionsAsync()
    {
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s =>
                s.SessionDate > DateTime.UtcNow &&
                s.Status == SessionStatus.Open &&
                s.RegisteredPlayersCount < s.MaxCapacity)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Session>> GetUserSessionsAsync(Guid userId)
    {
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s => s.Registrations.Any(r => r.UserId == userId))
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }
}