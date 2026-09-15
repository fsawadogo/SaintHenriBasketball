using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

// All player reservation/RSVP changes serialize on the same session row. A reservation
// and an RSVP from the same player occupy one place, never two.
public class ParticipationRepository(ApplicationDbContext db) : IParticipationRepository
{
    private async Task<IDbContextTransaction> LockAsync(Guid sessionId)
    {
        var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Sessions WITH (UPDLOCK, HOLDLOCK) WHERE Id = {sessionId}");
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private async Task<Session> SessionAsync(Guid id)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException("Session not found");
        if (session.Status is SessionStatus.Cancelled or SessionStatus.Completed)
            throw new ValidationException("This session is no longer open.");
        if (SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.StartTime)) <= DateTime.UtcNow)
            throw new ValidationException("Online booking changes close when the session starts. Please contact the club.");
        return session;
    }

    /// How long a waitlist offer stays open (capped at the session start).
    public static readonly TimeSpan OfferWindow = TimeSpan.FromHours(2);

    /// Players occupying a place: a reservation or an attending RSVP, counted once per player.
    internal static IQueryable<Guid> OccupantIds(ApplicationDbContext context, Guid id) => context.SessionRegistrations.Where(r => r.SessionId == id).Select(r => r.UserId)
        .Union(context.SessionAttendances.Where(a => a.SessionId == id && a.IsAttending).Select(a => a.UserId));

    private Task<int> OccupiedAsync(Guid id) => OccupantIds(db, id).CountAsync();

    private static void MakeOffer(Waitlist entry, DateTime sessionStartUtc)
    {
        entry.Status = WaitlistStatus.Offered;
        entry.OfferExpiresAt = new[] { DateTime.UtcNow.Add(OfferWindow), sessionStartUtc }.Min();
    }

    private async Task ExpireAsync(Guid sessionId)
    {
        var now = DateTime.UtcNow;
        var expired = await db.Waitlists.Where(w => w.SessionId == sessionId && w.Status == WaitlistStatus.Offered
            && (w.OfferExpiresAt == null || w.OfferExpiresAt <= now)).ToListAsync();
        foreach (var entry in expired) entry.Status = WaitlistStatus.Expired;
        await db.SaveChangesAsync();
    }

    private async Task<SessionRegistration> ReserveCoreAsync(Session session, Guid userId)
    {
        var existing = await db.SessionRegistrations.SingleOrDefaultAsync(r => r.SessionId == session.Id && r.UserId == userId);
        if (existing != null) return existing; // Safe to retry after a lost response.
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId) ?? throw new NotFoundException("User not found");
        await ExpireAsync(session.Id);
        var alreadyAttending = await db.SessionAttendances.AnyAsync(a => a.SessionId == session.Id && a.UserId == userId && a.IsAttending);
        var ownsOffer = await db.Waitlists.AnyAsync(w => w.SessionId == session.Id && w.UserId == userId && w.Status == WaitlistStatus.Offered);
        if (!alreadyAttending && !ownsOffer && await db.Waitlists.AnyAsync(w => w.SessionId == session.Id && w.Status == WaitlistStatus.Waiting))
            throw new ValidationException("The next available place is reserved for the waitlist. Join the list and wait for your offer.");
        var occupied = await OccupiedAsync(session.Id);
        var heldForOthers = await db.Waitlists.CountAsync(w => w.SessionId == session.Id && w.Status == WaitlistStatus.Offered && w.UserId != userId);
        if (!alreadyAttending && occupied + heldForOthers >= session.MaxCapacity)
            throw new ValidationException("No places are available. Join the waitlist for the next opening.");
        var entry = await db.Waitlists.FirstOrDefaultAsync(w => w.SessionId == session.Id && w.UserId == userId
            && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Offered));
        if (entry != null) entry.Status = WaitlistStatus.Accepted;
        var registration = new SessionRegistration(userId, session.Id, user.PaymentPlan);
        db.SessionRegistrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    private async Task RefreshCountAsync(Session session)
    {
        session.RegisteredPlayersCount = await OccupiedAsync(session.Id);
        session.Status = session.RegisteredPlayersCount >= session.MaxCapacity ? SessionStatus.Full : SessionStatus.Open;
        await db.SaveChangesAsync();
    }

    public async Task<SessionRegistration> ReserveAsync(Guid sessionId, Guid userId)
    {
        await using var tx = await LockAsync(sessionId);
        var session = await SessionAsync(sessionId);
        var registration = await ReserveCoreAsync(session, userId);
        await RefreshCountAsync(session);
        await tx.CommitAsync();
        return registration;
    }

    public async Task CancelAsync(Guid sessionId, Guid userId)
    {
        await using var tx = await LockAsync(sessionId);
        var session = await SessionAsync(sessionId);
        await CancelCoreAsync(sessionId, userId);
        await RefreshCountAsync(session);
        await tx.CommitAsync();
    }

    private async Task CancelCoreAsync(Guid sessionId, Guid userId)
    {
        var registration = await db.SessionRegistrations.SingleOrDefaultAsync(r => r.SessionId == sessionId && r.UserId == userId);
        if (registration != null) db.SessionRegistrations.Remove(registration);
        var attendance = await db.SessionAttendances.SingleOrDefaultAsync(a => a.SessionId == sessionId && a.UserId == userId);
        if (attendance != null) { attendance.IsAttending = false; attendance.CheckInTime = null; attendance.LastUpdated = DateTime.UtcNow; }
        await db.SaveChangesAsync();
    }

    public async Task<SessionAttendance> SetAttendanceAsync(Guid sessionId, Guid userId, bool attending, string? notes, string? reason)
    {
        await using var tx = await LockAsync(sessionId);
        var session = await SessionAsync(sessionId);
        if (attending) await ReserveCoreAsync(session, userId);
        else await CancelCoreAsync(sessionId, userId);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId) ?? throw new NotFoundException("User not found");
        var record = await db.SessionAttendances.SingleOrDefaultAsync(a => a.SessionId == sessionId && a.UserId == userId);
        if (record == null)
        {
            record = new SessionAttendance { Id = Guid.NewGuid(), SessionId = sessionId, UserId = userId, CreatedOn = DateTime.UtcNow };
            db.SessionAttendances.Add(record);
        }
        record.Session = session; record.User = user;
        record.IsAttending = attending; record.Notes = notes ?? record.Notes;
        record.UpdateReason = reason; record.LastUpdated = DateTime.UtcNow;
        // RSVP is not physical check-in. Only the check-in workflow sets CheckInTime.
        if (!attending) record.CheckInTime = null;
        await db.SaveChangesAsync();
        await RefreshCountAsync(session);
        await tx.CommitAsync();
        return record;
    }

    public async Task<SessionAttendance> CheckInAsync(Guid sessionId, Guid userId)
    {
        await using var tx = await LockAsync(sessionId);
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new NotFoundException("Session not found");
        var now = DateTime.UtcNow;
        var start = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.StartTime));
        var end = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.EndTime));
        if (session.Status is SessionStatus.Cancelled or SessionStatus.Completed || now < start.AddMinutes(-30) || now >= end)
            throw new ValidationException("Check-in opens 30 minutes before the session and closes when it ends.");
        await ReserveCoreAsync(session, userId);
        var record = await db.SessionAttendances.SingleOrDefaultAsync(a => a.SessionId == sessionId && a.UserId == userId);
        if (record == null)
        {
            record = new SessionAttendance { Id = Guid.NewGuid(), SessionId = sessionId, UserId = userId, CreatedOn = now };
            db.SessionAttendances.Add(record);
        }
        record.IsAttending = true;
        record.CheckInTime ??= now;
        record.LastUpdated = now;
        await db.SaveChangesAsync();
        await RefreshCountAsync(session);
        await tx.CommitAsync();
        return record;
    }

    public async Task LeaveWaitlistAsync(Guid sessionId, Guid userId)
    {
        await using var tx = await LockAsync(sessionId);
        var entries = await db.Waitlists.Where(w => w.SessionId == sessionId && w.UserId == userId
            && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Offered)).ToListAsync();
        foreach (var entry in entries) entry.Status = WaitlistStatus.Cancelled;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<Waitlist> JoinWaitlistAsync(Guid sessionId, Guid userId, string? notes)
    {
        await using var tx = await LockAsync(sessionId);
        var session = await SessionAsync(sessionId);
        if (await db.SessionRegistrations.AnyAsync(r => r.SessionId == sessionId && r.UserId == userId))
            throw new ValidationException("You already have a place in this session.");
        // One row per player and session (unique index): reuse it rather than inserting a second one.
        var existing = await db.Waitlists.Include(w => w.User).FirstOrDefaultAsync(w => w.SessionId == sessionId && w.UserId == userId);
        if (existing is { Status: WaitlistStatus.Waiting or WaitlistStatus.Offered }) { await tx.CommitAsync(); return existing; }
        var position = (await db.Waitlists.Where(w => w.SessionId == sessionId).MaxAsync(w => (int?)w.Position) ?? 0) + 1;
        if (existing != null)
        {
            // A player who left, let an offer expire, or gave up a place joins again at the back of the line.
            existing.Status = WaitlistStatus.Waiting;
            existing.Position = position;
            existing.RegistrationDate = DateTime.UtcNow;
            existing.OfferExpiresAt = null;
            existing.Notes = notes;
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return existing;
        }
        var entry = new Waitlist(userId, sessionId, position) { Notes = notes };
        db.Waitlists.Add(entry); await db.SaveChangesAsync(); await tx.CommitAsync();
        return entry;
    }

    public async Task<Waitlist?> OfferNextAsync(Guid sessionId)
    {
        await using var tx = await LockAsync(sessionId);
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId);
        await ExpireAsync(sessionId);
        if (session == null || session.Status is SessionStatus.Cancelled or SessionStatus.Completed) { await tx.CommitAsync(); return null; }
        var start = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.StartTime));
        if (start <= DateTime.UtcNow) { await tx.CommitAsync(); return null; }
        var held = await db.Waitlists.CountAsync(w => w.SessionId == sessionId && w.Status == WaitlistStatus.Offered);
        if (await OccupiedAsync(sessionId) + held >= session.MaxCapacity) { await tx.CommitAsync(); return null; }
        var next = await db.Waitlists.Include(w => w.User).Where(w => w.SessionId == sessionId && w.Status == WaitlistStatus.Waiting
            && !db.SessionRegistrations.Any(r => r.SessionId == sessionId && r.UserId == w.UserId)).OrderBy(w => w.Position).ThenBy(w => w.RegistrationDate).FirstOrDefaultAsync();
        if (next != null) { MakeOffer(next, start); await db.SaveChangesAsync(); }
        await tx.CommitAsync(); return next;
    }

    public async Task<IReadOnlyList<Guid>> GetWaitlistSessionIdsAsync() => await db.Waitlists
        .Where(w => w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Offered).Select(w => w.SessionId).Distinct().ToListAsync();

    // ---- Admin waitlist management (waitlist-admin). Same session-row lock as every other waitlist change. ----

    private async Task<Guid> EntrySessionIdAsync(Guid entryId) =>
        await db.Waitlists.Where(w => w.Id == entryId).Select(w => (Guid?)w.SessionId).SingleOrDefaultAsync()
        ?? throw new NotFoundException("Waitlist entry not found");

    private async Task<Waitlist> LockedEntryAsync(Guid entryId) =>
        await db.Waitlists.Include(w => w.User).SingleOrDefaultAsync(w => w.Id == entryId)
        ?? throw new NotFoundException("Waitlist entry not found");

    private static bool OnWaitlist(Waitlist entry) => entry.Status is WaitlistStatus.Waiting or WaitlistStatus.Offered;

    /// Gives the waiting (and offered) entries positions 1..N in line order, optionally moving one entry to a
    /// 1-based target first. Entries that left the line get position 0, so a new join lands at N + 1.
    private async Task<int> RenumberAsync(Guid sessionId, Guid? moveEntryId = null, int target = 0)
    {
        var rows = await db.Waitlists.Where(w => w.SessionId == sessionId).ToListAsync();
        var line = rows.Where(OnWaitlist).OrderBy(w => w.Position).ThenBy(w => w.RegistrationDate).ThenBy(w => w.Id).ToList();
        var placed = 0;
        if (moveEntryId != null)
        {
            var moving = line.Single(w => w.Id == moveEntryId);
            line.Remove(moving);
            placed = Math.Clamp(target, 1, line.Count + 1);
            line.Insert(placed - 1, moving);
        }
        for (var i = 0; i < line.Count; i++) line[i].Position = i + 1;
        foreach (var gone in rows.Where(w => !OnWaitlist(w))) gone.Position = 0;
        await db.SaveChangesAsync();
        return placed;
    }

    public async Task<(Waitlist Entry, bool HadOpenSpot)> OfferEntryAsync(Guid entryId)
    {
        var sessionId = await EntrySessionIdAsync(entryId);
        await using var tx = await LockAsync(sessionId);
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId) ?? throw new NotFoundException("Session not found");
        await ExpireAsync(sessionId);
        var entry = await LockedEntryAsync(entryId);
        if (entry.Status == WaitlistStatus.Offered)
            throw new ValidationException("This player already has an outstanding offer.");
        if (entry.Status != WaitlistStatus.Waiting)
            throw new ValidationException($"Only waiting players can receive an offer. This entry is {entry.Status}.");
        if (session.Status is SessionStatus.Cancelled or SessionStatus.Completed)
            throw new ValidationException("This session is no longer open.");
        var start = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.StartTime));
        if (start <= DateTime.UtcNow)
            throw new ValidationException("Offers close when the session starts.");
        if (await db.SessionRegistrations.AnyAsync(r => r.SessionId == sessionId && r.UserId == entry.UserId))
            throw new ValidationException("This player already has a place in this session.");
        var held = await db.Waitlists.CountAsync(w => w.SessionId == sessionId && w.Status == WaitlistStatus.Offered);
        var hadOpenSpot = await OccupiedAsync(sessionId) + held < session.MaxCapacity;
        MakeOffer(entry, start);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (entry, hadOpenSpot);
    }

    public async Task<Waitlist> RemoveWaitlistEntryAsync(Guid entryId)
    {
        var sessionId = await EntrySessionIdAsync(entryId);
        await using var tx = await LockAsync(sessionId);
        await ExpireAsync(sessionId);
        var entry = await LockedEntryAsync(entryId);
        if (!OnWaitlist(entry))
            throw new ValidationException($"This entry is no longer on the waitlist ({entry.Status}).");
        db.Waitlists.Remove(entry);
        await db.SaveChangesAsync();
        await RenumberAsync(sessionId);
        await tx.CommitAsync();
        return entry;
    }

    public async Task<(Guid SessionId, int Position)> MoveWaitlistEntryAsync(Guid entryId, int position)
    {
        var sessionId = await EntrySessionIdAsync(entryId);
        await using var tx = await LockAsync(sessionId);
        await ExpireAsync(sessionId);
        var entry = await LockedEntryAsync(entryId);
        if (!OnWaitlist(entry))
            throw new ValidationException($"This entry is no longer on the waitlist ({entry.Status}).");
        var placed = await RenumberAsync(sessionId, entryId, position);
        await tx.CommitAsync();
        return (sessionId, placed);
    }
}
