using SaintHenriBasketball.Application.Helpers;
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
        var today = SessionTimeHelper.MontrealToday(); // Montreal's date: the server runs on UTC
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s => s.SessionDate.Date >= today)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Session>> GetAvailableSessionsAsync()
    {
        var today = SessionTimeHelper.MontrealToday(); // Montreal's date: the server runs on UTC
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s =>
                s.SessionDate.Date >= today &&
                (s.Status == SessionStatus.Open || s.Status == SessionStatus.Full))
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

    /// How many date-ordered rows the "next session" lookups materialise before picking the first
    /// session that hasn't ended. Only sessions dated today can already have ended, so any bound
    /// above the number of sessions in a single day is enough; the gym runs a handful.
    private const int NextSessionCandidateLimit = 50;

    /// The first of <paramref name="candidates"/> whose end time hasn't passed. The end-time rule needs
    /// `CombineLocal`/`ToUtc`, which don't translate to SQL, so it runs in memory over a bounded page.
    /// Ordered here too: SQL orders by date alone, which leaves same-day sessions in an arbitrary order.
    private static Session? FirstNotEnded(IEnumerable<Session> candidates)
    {
        var nowUtc = DateTime.UtcNow;
        return candidates
            .Where(s => !SessionTimeHelper.HasEnded(s.SessionDate, s.EndTime, nowUtc))
            .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public async Task<Session?> GetClosestSessionAsync()
    {
        var today = SessionTimeHelper.MontrealToday(); // Montreal's date: the server runs on UTC
        var candidates = await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s => s.SessionDate.Date >= today)
            .OrderBy(s => s.SessionDate)
            .Take(NextSessionCandidateLimit)
            .ToListAsync();
        return FirstNotEnded(candidates);
    }

    public async Task<Session?> GetNextSessionAsync()
    {
        var today = SessionTimeHelper.MontrealToday(); // Montreal's date: the server runs on UTC
        var candidates = await _context.Sessions
            .Include(s => s.Registrations)
            .ThenInclude(r => r.User)
            .Where(s =>
                s.SessionDate.Date >= today &&
                s.Status == SessionStatus.Open &&
                s.RegisteredPlayersCount < s.MaxCapacity)
            .OrderBy(s => s.SessionDate)
            .Take(NextSessionCandidateLimit)
            .ToListAsync();
        return FirstNotEnded(candidates);
    }

    public async Task<IReadOnlyList<Session>> GetAllSessionsAsync()
    {
        return await _context.Sessions
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();
    }

    public Task<int> CountSessionsBetweenAsync(DateTime from, DateTime to) =>
        _context.Sessions.CountAsync(s => s.SessionDate >= from && s.SessionDate <= to);

    public async Task<IReadOnlyList<Session>> GetSessionsBetweenDatesAsync(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.Date;
        var toExclusive = toDate.Date.AddDays(1);
        return await _context.Sessions
            .Include(s => s.Registrations)
            .Where(s => s.SessionDate >= from && s.SessionDate < toExclusive)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<SessionDeletionImpact?> GetDeletionImpactAsync(Guid sessionId)
    {
        if (!await _context.Sessions.AnyAsync(s => s.Id == sessionId)) return null;
        return new SessionDeletionImpact(
            await _context.SessionRegistrations.CountAsync(r => r.SessionId == sessionId),
            await _context.SessionAttendances.CountAsync(a => a.SessionId == sessionId),
            await _context.Waitlists.CountAsync(w => w.SessionId == sessionId),
            await _context.SessionFeedbacks.CountAsync(f => f.SessionId == sessionId),
            await _context.SessionRecaps.CountAsync(r => r.SessionId == sessionId),
            await _context.Payments.CountAsync(p => p.SessionId == sessionId));
    }

    public async Task<bool> DeleteWithDependentsAsync(Guid sessionId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        // Checkout locks the session row too, so no payment can attach between this check and the delete.
        var locked = await _context.Sessions
            .FromSqlInterpolated($"SELECT * FROM Sessions WITH (UPDLOCK, HOLDLOCK) WHERE Id = {sessionId}")
            .AsNoTracking()
            .ToListAsync();
        if (locked.Count == 0 || await _context.Payments.AnyAsync(p => p.SessionId == sessionId)) return false;

        await _context.SessionFeedbacks.Where(f => f.SessionId == sessionId).ExecuteDeleteAsync();
        await _context.SessionRecaps.Where(r => r.SessionId == sessionId).ExecuteDeleteAsync();
        await _context.Waitlists.Where(w => w.SessionId == sessionId).ExecuteDeleteAsync();
        await _context.SessionAttendances.Where(a => a.SessionId == sessionId).ExecuteDeleteAsync();
        await _context.SessionRegistrations.Where(r => r.SessionId == sessionId).ExecuteDeleteAsync();
        await _context.Sessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<IReadOnlyList<SessionRosterEntry>> GetRosterAsync(Guid sessionId)
    {
        var rows = await _context.SessionRegistrations.AsNoTracking()
            .Where(r => r.SessionId == sessionId)
            .Select(r => new
            {
                r.UserId,
                r.User.FirstName,
                r.User.LastName,
                // Their own answer to a reminder, not whether they turned up.
                Confirmed = _context.SessionAttendances
                    .Any(a => a.SessionId == sessionId && a.UserId == r.UserId && a.IsAttending),
            })
            .ToListAsync();

        return rows
            .Select(r => new SessionRosterEntry(r.UserId, r.FirstName ?? string.Empty, r.LastName ?? string.Empty, r.Confirmed))
            .ToList();
    }
}
