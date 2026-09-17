using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SeasonDashboardRepository(ApplicationDbContext db) : ISeasonDashboardRepository
{
    public async Task<IReadOnlyList<Season>> GetSeasonsAsync() =>
        await db.Seasons.AsNoTracking().OrderByDescending(s => s.StartDate).ToListAsync();

    public async Task<Season?> GetSeasonAsync(Guid seasonId) =>
        await db.Seasons.AsNoTracking().SingleOrDefaultAsync(s => s.Id == seasonId);

    public async Task<Season?> GetCurrentOrNextSeasonAsync(DateTime todayLocal)
    {
        var running = await db.Seasons.AsNoTracking()
            .Where(s => s.StartDate <= todayLocal && s.EndDate >= todayLocal)
            .OrderBy(s => s.StartDate)
            .FirstOrDefaultAsync();
        if (running != null) return running;

        var next = await db.Seasons.AsNoTracking()
            .Where(s => s.StartDate > todayLocal)
            .OrderBy(s => s.StartDate)
            .FirstOrDefaultAsync();
        return next ?? await db.Seasons.AsNoTracking().OrderByDescending(s => s.StartDate).FirstOrDefaultAsync();
    }

    /// Season payments are linked by SeasonId. Drop-ins are linked to a session, so they belong to the
    /// season whose dates cover that session; older drop-ins with no session are left out rather than
    /// guessed at, which would move money between seasons.
    public async Task<IReadOnlyList<Payment>> GetSeasonPaymentsAsync(Guid seasonId, DateTime startDate, DateTime endDateExclusive)
    {
        var sessionIds = db.Sessions
            .Where(s => s.SessionDate >= startDate && s.SessionDate < endDateExclusive)
            .Select(s => s.Id);

        return await db.Payments.AsNoTracking()
            .Where(p => p.SeasonId == seasonId || (p.SessionId != null && sessionIds.Contains(p.SessionId.Value)))
            .ToListAsync();
    }

    public async Task<(int Paid, int UnpaidChoices)> GetPassCountsAsync(Guid seasonId)
    {
        // A pass is held by a completed season payment, the same rule the player-facing endpoint uses.
        var paidUserIds = await db.Payments.AsNoTracking()
            .Where(p => p.SeasonId == seasonId && p.Plan == PaymentPlan.Season && p.Status == PaymentStatus.Completed)
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync();

        var unpaidChoices = await db.SeasonPlanChoices.AsNoTracking()
            .Where(c => c.SeasonId == seasonId && c.Plan == PaymentPlan.Season && !paidUserIds.Contains(c.UserId))
            .Select(c => c.UserId)
            .Distinct()
            .CountAsync();

        return (paidUserIds.Count, unpaidChoices);
    }

    public async Task<IReadOnlyList<SeasonDashboardSessionRow>> GetSessionRowsAsync(Guid seasonId, DateTime startDate, DateTime endDateExclusive)
    {
        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.SessionDate >= startDate && s.SessionDate < endDateExclusive)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
            .ToListAsync();
        if (sessions.Count == 0) return Array.Empty<SeasonDashboardSessionRow>();

        var ids = sessions.Select(s => s.Id).ToList();

        var reserved = await db.SessionRegistrations.AsNoTracking()
            .Where(r => ids.Contains(r.SessionId))
            .GroupBy(r => r.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count);

        var attended = await db.SessionAttendances.AsNoTracking()
            .Where(a => ids.Contains(a.SessionId) && a.IsAttending)
            .GroupBy(a => a.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count);

        var dropIns = await db.Payments.AsNoTracking()
            .Where(p => p.SessionId != null && ids.Contains(p.SessionId.Value) && p.Plan == PaymentPlan.DropIn)
            .GroupBy(p => p.SessionId!.Value)
            .Select(g => new
            {
                SessionId = g.Key,
                Collected = g.Where(p => p.Status == PaymentStatus.Completed).Sum(p => (decimal?)p.Amount) ?? 0m,
                Pending = g.Count(p => p.Status == PaymentStatus.Pending),
            })
            .ToDictionaryAsync(x => x.SessionId);

        return sessions.Select(s => new SeasonDashboardSessionRow(
            s,
            reserved.GetValueOrDefault(s.Id),
            attended.GetValueOrDefault(s.Id),
            dropIns.TryGetValue(s.Id, out var money) ? money.Collected : 0m,
            dropIns.TryGetValue(s.Id, out var counts) ? counts.Pending : 0)).ToList();
    }
}
