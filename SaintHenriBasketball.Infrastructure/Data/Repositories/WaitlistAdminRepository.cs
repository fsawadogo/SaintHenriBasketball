using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class WaitlistAdminRepository(ApplicationDbContext db) : IWaitlistAdminRepository
{
    public async Task<WaitlistSessionSnapshot?> GetSessionWaitlistAsync(Guid sessionId, DateTime nowUtc)
    {
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) return null;
        var occupied = await ParticipationRepository.OccupantIds(db, sessionId).CountAsync();
        // An offer past its expiry is already over: every locked change marks it Expired before doing anything else.
        var entries = await db.Waitlists.AsNoTracking().Include(w => w.User)
            .Where(w => w.SessionId == sessionId && (w.Status == WaitlistStatus.Waiting || (w.Status == WaitlistStatus.Offered && w.OfferExpiresAt > nowUtc)))
            .OrderBy(w => w.Position).ThenBy(w => w.RegistrationDate).ThenBy(w => w.Id)
            .ToListAsync();

        // There is no OfferSentAt column. An offer expires at min(sent + OfferWindow, session start), so when the expiry is
        // before the start the send time is exact. Otherwise use the in-app offer notification created with it, if the
        // player allows those; failing both, it is unknown.
        var start = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.StartTime));
        var offeredUserIds = entries.Where(e => e.Status == WaitlistStatus.Offered).Select(e => e.UserId).ToList();
        var bookUrl = $"/sessions/{sessionId}/book";
        var notices = offeredUserIds.Count == 0
            ? new List<(Guid UserId, DateTime CreatedOn)>()
            : (await db.Notifications.AsNoTracking()
                .Where(n => n.Type == NotificationType.WaitlistOffer && n.Url == bookUrl && offeredUserIds.Contains(n.UserId))
                .Select(n => new { n.UserId, n.CreatedOn }).ToListAsync())
                .Select(n => (n.UserId, n.CreatedOn)).ToList();

        DateTime? SentAt(Waitlist entry)
        {
            if (entry.Status != WaitlistStatus.Offered || entry.OfferExpiresAt is not DateTime expires) return null;
            if (expires < start) return expires - ParticipationRepository.OfferWindow;
            var notified = notices.Where(n => n.UserId == entry.UserId && n.CreatedOn <= expires && n.CreatedOn >= expires - ParticipationRepository.OfferWindow)
                .Select(n => (DateTime?)n.CreatedOn).Max();
            return notified;
        }

        return new WaitlistSessionSnapshot(session, occupied, entries.Select(e => new WaitlistLineEntry(e, SentAt(e))).ToList());
    }

    public async Task<IReadOnlyList<WaitlistDemandRow>> GetDemandAsync(DateTime fromDate, DateTime toDateExclusive, DateTime nowUtc)
    {
        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.SessionDate >= fromDate && s.SessionDate < toDateExclusive && s.Status != SessionStatus.Cancelled)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
            .ToListAsync();
        if (sessions.Count == 0) return Array.Empty<WaitlistDemandRow>();
        var ids = sessions.Select(s => s.Id).ToList();

        // Same rule as ParticipationRepository.OccupantIds: a reservation or attending RSVP, once per player.
        var booked = await db.SessionRegistrations.Where(r => ids.Contains(r.SessionId)).Select(r => new { r.SessionId, r.UserId }).ToListAsync();
        var attending = await db.SessionAttendances.Where(a => ids.Contains(a.SessionId) && a.IsAttending).Select(a => new { a.SessionId, a.UserId }).ToListAsync();
        var occupied = booked.Concat(attending).Distinct().GroupBy(x => x.SessionId).ToDictionary(g => g.Key, g => g.Count());

        var lines = await db.Waitlists
            .Where(w => ids.Contains(w.SessionId) && (w.Status == WaitlistStatus.Waiting || (w.Status == WaitlistStatus.Offered && w.OfferExpiresAt > nowUtc)))
            .GroupBy(w => w.SessionId)
            .Select(g => new { SessionId = g.Key, Waiting = g.Count(w => w.Status == WaitlistStatus.Waiting), Offers = g.Count(w => w.Status == WaitlistStatus.Offered) })
            .ToDictionaryAsync(x => x.SessionId);

        return sessions.Select(s => new WaitlistDemandRow(s,
            occupied.GetValueOrDefault(s.Id),
            lines.TryGetValue(s.Id, out var line) ? line.Waiting : 0,
            line?.Offers ?? 0)).ToList();
    }
}
