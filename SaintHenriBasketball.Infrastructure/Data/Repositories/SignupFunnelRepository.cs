using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SignupFunnelRepository(ApplicationDbContext db) : ISignupFunnelRepository
{
    /// Four queries, whatever the number of players: the milestones are grouped in the database.
    public async Task<IReadOnlyList<SignupFunnelRow>> GetRowsAsync(DateTime fromUtc, DateTime toUtcExclusive)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => !u.IsAdmin && u.CreatedOn >= fromUtc && u.CreatedOn < toUtcExclusive)
            .Select(u => new { u.Id, u.CreatedOn, u.EmailConfirmed, u.IsDeactivated })
            .ToListAsync();
        if (users.Count == 0) return Array.Empty<SignupFunnelRow>();

        var ids = users.Select(u => u.Id).ToList();

        var firstReservation = await db.SessionRegistrations.AsNoTracking()
            .Where(r => ids.Contains(r.UserId))
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, At = g.Min(r => r.RegistrationDate) })
            .ToDictionaryAsync(x => x.UserId, x => x.At);

        // Money actually received: a pending payment is an intention, not a payment.
        var firstPayment = await db.Payments.AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, At = g.Min(p => p.PaymentDate) })
            .ToDictionaryAsync(x => x.UserId, x => x.At);

        // Played, not merely said yes: the session must have taken place.
        var firstAttendance = await db.SessionAttendances.AsNoTracking()
            .Where(a => ids.Contains(a.UserId) && a.IsAttending)
            .Join(db.Sessions.AsNoTracking().Where(s => s.Status != SessionStatus.Cancelled),
                a => a.SessionId, s => s.Id, (a, s) => new { a.UserId, s.SessionDate })
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, At = g.Min(x => x.SessionDate) })
            .ToDictionaryAsync(x => x.UserId, x => x.At);

        return users.Select(u => new SignupFunnelRow(
            u.Id,
            u.CreatedOn,
            u.EmailConfirmed,
            u.IsDeactivated,
            firstReservation.TryGetValue(u.Id, out var reserved) ? reserved : null,
            firstPayment.TryGetValue(u.Id, out var paid) ? paid : null,
            firstAttendance.TryGetValue(u.Id, out var played) ? played : null)).ToList();
    }
}
