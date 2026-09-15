using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class OutstandingBalancesRepository : IOutstandingBalancesRepository
{
    private const int MaxPageSize = 200;
    private readonly ApplicationDbContext _context;

    public OutstandingBalancesRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Season>> GetBillableSeasonsAsync(DateTime todayLocal)
    {
        var today = todayLocal.Date;
        return await _context.Seasons.AsNoTracking()
            .Where(s => s.Price > 0 && s.EndDate >= today && (s.StartDate <= today || s.Status == SeasonStatus.Open))
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SeasonRegistration>> GetSeasonRegistrationsAsync(IReadOnlyCollection<Guid> seasonIds)
    {
        if (seasonIds.Count == 0) return Array.Empty<SeasonRegistration>();
        return await _context.SeasonRegistrations.AsNoTracking()
            .Include(r => r.User)
            .Where(r => seasonIds.Contains(r.SeasonId))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetActiveSeasonPlanPlayersAsync() =>
        await _context.Users.AsNoTracking()
            .Where(u => u.PaymentPlan == PaymentPlan.Season && !u.IsDeactivated)
            .ToListAsync();

    public async Task<IReadOnlyList<Payment>> GetSeasonPaymentsAsync(IReadOnlyCollection<Guid> seasonIds)
    {
        if (seasonIds.Count == 0) return Array.Empty<Payment>();
        return await _context.Payments.AsNoTracking()
            .Where(p => p.SeasonId != null && seasonIds.Contains(p.SeasonId.Value))
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<Guid>> GetUsersWithUnlinkedSeasonPaymentsAsync() =>
        await _context.Payments.AsNoTracking()
            .Where(p => p.Plan == PaymentPlan.Season && p.SeasonId == null
                && p.Status != PaymentStatus.Refunded && p.Status != PaymentStatus.Failed)
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync();

    public async Task<IReadOnlyList<ReminderLastSent>> GetLastSentAsync(IReadOnlyCollection<Guid> userIds, DateTime? since = null)
    {
        if (userIds.Count == 0) return Array.Empty<ReminderLastSent>();
        var query = _context.Set<ReminderLog>().AsNoTracking()
            .Where(l => l.Status == ReminderStatuses.Sent && userIds.Contains(l.UserId));
        if (since is DateTime from) query = query.Where(l => l.SentAt >= from);

        var rows = await query
            .GroupBy(l => new { l.UserId, l.Kind, l.PaymentId, l.SeasonId })
            .Select(g => new { g.Key.UserId, g.Key.Kind, g.Key.PaymentId, g.Key.SeasonId, SentAt = g.Max(l => l.SentAt) })
            .ToListAsync();
        return rows
            .Select(r => new ReminderLastSent(r.UserId, r.Kind, r.PaymentId, r.SeasonId, DateTime.SpecifyKind(r.SentAt, DateTimeKind.Utc)))
            .ToList();
    }

    public async Task AddLogsAsync(IEnumerable<ReminderLog> logs)
    {
        _context.Set<ReminderLog>().AddRange(logs);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> UserExistsAsync(Guid userId) =>
        await _context.Users.AnyAsync(u => u.Id == userId);

    public async Task<(IReadOnlyList<ReminderLogEntry> Items, int Total)> SearchLogsAsync(Guid? userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var query = _context.Set<ReminderLog>().AsNoTracking();
        if (userId is Guid id) query = query.Where(l => l.UserId == id);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(l => l.SentAt)
            .ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_context.Users, l => l.UserId, u => u.Id, (l, u) => new { Log = l, u.FirstName, u.LastName, u.Email })
            .ToListAsync();
        return (rows.Select(r => new ReminderLogEntry(r.Log, r.FirstName, r.LastName, r.Email)).ToList(), total);
    }
}
