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

    public async Task<Session?> GetByIdAsync(Guid id)
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
        var now = DateTime.Now; // Use local time
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s => IsSessionStillRelevant(s, now))
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Session>> GetAvailableSessionsAsync()
    {
        var now = DateTime.Now; // Use local time
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s =>
                IsSessionStillRelevant(s, now) &&
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
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Session>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s => ids.Contains(s.Id))
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<int> GetRegistrationCountAsync(Guid sessionId)
    {
        return await _context.Sessions
            .Where(s => s.Id == sessionId)
            .Select(s => s.RegisteredPlayersCount)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Sessions.AnyAsync(s => s.Id == id);
    }

    public async Task<Session?> GetClosestSessionAsync()
    {
        var now = DateTime.Now; // Use local time
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s => IsSessionStillRelevant(s, now))
            .OrderBy(s => s.SessionDate)
            .FirstOrDefaultAsync();
    }

    public async Task<Session?> GetNextSessionAsync()
    {
        var now = DateTime.Now; // Use local time
        return await _context.Sessions
            .Include(s => s.Registrations)
            .ThenInclude(r => r.User)
            .Where(s =>
                IsSessionStillRelevant(s, now) &&
                s.Status == SessionStatus.Open &&
                s.RegisteredPlayersCount < s.MaxCapacity)
            .OrderBy(s => s.SessionDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<Session>> GetAllSessionsAsync()
    {
        return await _context.Sessions
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();
    }

    /// <summary>
    /// Determines if a session should still be displayed based on local time.
    /// Sessions are removed 1 hour after their end time on the day of the session.
    /// </summary>
    private static bool IsSessionStillRelevant(Session session, DateTime now)
    {
        // If session is in the future, always show it
        if (session.SessionDate.Date > now.Date)
        {
            return true;
        }

        // If session is in the past (not today), don't show it
        if (session.SessionDate.Date < now.Date)
        {
            return false;
        }

        // Session is today - check if we're within 1 hour of end time
        if (session.SessionDate.Date == now.Date)
        {
            // Parse the end time (format is "HH:mm" like "12:00")
            if (TimeSpan.TryParse(session.EndTime + ":00", out var endTime))
            {
                // Create the session end datetime for today
                var sessionEndDateTime = session.SessionDate.Date.Add(endTime);
                
                // Check if current time is within 1 hour of session end time
                var oneHourAfterEnd = sessionEndDateTime.AddHours(1);
                return now <= oneHourAfterEnd;
            }
        }

        return false;
    }
}