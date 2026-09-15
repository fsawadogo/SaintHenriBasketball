using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class PlayerTimelineRepository : IPlayerTimelineRepository
{
    private static readonly DateTime Missing = PlayerTimelineTimes.MissingBefore;

    // Each key is the time of the row's newest timeline event, exactly as the service dates the events.
    // Payment events: created (CreatedAt, or PaymentDate when unset), completed (PaymentDate), refunded (RefundedOn or PaymentDate).
    private static readonly Expression<Func<Payment, DateTime>> PaymentLatest = p =>
        p.Status == PaymentStatus.Refunded && p.RefundedOn != null && p.RefundedOn > p.PaymentDate
            && p.RefundedOn > (p.CreatedAt < Missing ? p.PaymentDate : p.CreatedAt)
            ? p.RefundedOn.Value
            : (p.Status == PaymentStatus.Completed || p.Status == PaymentStatus.Refunded) && p.PaymentDate > (p.CreatedAt < Missing ? p.PaymentDate : p.CreatedAt)
                ? p.PaymentDate
                : (p.CreatedAt < Missing ? p.PaymentDate : p.CreatedAt);

    // Attendance events: the answer (LastUpdated, or CreatedOn when unset) and the check-in.
    private static readonly Expression<Func<SessionAttendance, DateTime>> AttendanceLatest = a =>
        a.CheckInTime != null && a.CheckInTime > (a.LastUpdated < Missing ? a.CreatedOn : a.LastUpdated)
            ? a.CheckInTime.Value
            : (a.LastUpdated < Missing ? a.CreatedOn : a.LastUpdated);

    private readonly ApplicationDbContext _context;

    public PlayerTimelineRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlayerTimelineSubject?> GetPlayerAsync(Guid userId) =>
        await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new PlayerTimelineSubject(u.Id, u.FirstName, u.LastName, u.Email, u.AdminNotes, u.IsDeactivated, u.AnonymizedOn))
            .FirstOrDefaultAsync();

    public async Task<PlayerTimelineSourceCounts> CountAsync(Guid userId, string? email)
    {
        var payments = await _context.Payments
            .Where(p => p.UserId == userId)
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                All = g.Count(),
                Completed = g.Count(p => p.Status == PaymentStatus.Completed || p.Status == PaymentStatus.Refunded),
                Refunded = g.Count(p => p.Status == PaymentStatus.Refunded),
            })
            .FirstOrDefaultAsync();
        var attendance = await _context.SessionAttendances
            .Where(a => a.UserId == userId)
            .GroupBy(a => a.UserId)
            .Select(g => new { All = g.Count(), CheckIns = g.Count(a => a.CheckInTime != null) })
            .FirstOrDefaultAsync();

        return new PlayerTimelineSourceCounts(
            payments?.All ?? 0,
            payments?.Completed ?? 0,
            payments?.Refunded ?? 0,
            await _context.AccountCredits.CountAsync(c => c.UserId == userId),
            await _context.ReferralRedemptions.CountAsync(r => r.ReferrerUserId == userId),
            await _context.ReferralRedemptions.CountAsync(r => r.RefereeUserId == userId),
            await _context.WaiverAcceptances.CountAsync(w => w.UserId == userId),
            attendance?.All ?? 0,
            attendance?.CheckIns ?? 0,
            email == null ? 0 : await _context.EmailLogs.CountAsync(l => l.Recipient == email),
            await _context.Notifications.CountAsync(n => n.UserId == userId),
            await _context.AuditLogs.CountAsync(l => l.EntityId == userId));
    }

    public Task<IReadOnlyList<Payment>> GetNewestPaymentsAsync(Guid userId, int limit) =>
        NewestAsync(_context.Payments.AsNoTracking().Include(p => p.Session).Include(p => p.Season).Where(p => p.UserId == userId), PaymentLatest, limit);

    public Task<IReadOnlyList<AccountCredit>> GetNewestCreditsAsync(Guid userId, int limit) =>
        NewestAsync(_context.AccountCredits.AsNoTracking().Where(c => c.UserId == userId), c => c.CreatedAt, limit);

    public Task<IReadOnlyList<ReferralRedemption>> GetNewestReferralsAsync(Guid userId, int limit) =>
        NewestAsync(_context.ReferralRedemptions.AsNoTracking().Where(r => r.ReferrerUserId == userId || r.RefereeUserId == userId), r => r.RedeemedOn, limit);

    public Task<IReadOnlyList<WaiverAcceptance>> GetNewestWaiverAcceptancesAsync(Guid userId, int limit) =>
        NewestAsync(_context.WaiverAcceptances.AsNoTracking().Where(w => w.UserId == userId), w => w.AcceptedAt, limit);

    public Task<IReadOnlyList<SessionAttendance>> GetNewestAttendanceAsync(Guid userId, int limit) =>
        NewestAsync(_context.SessionAttendances.AsNoTracking().Include(a => a.Session).Where(a => a.UserId == userId), AttendanceLatest, limit);

    public Task<IReadOnlyList<EmailLog>> GetNewestEmailLogsAsync(string email, int limit) =>
        NewestAsync(_context.EmailLogs.AsNoTracking().Where(l => l.Recipient == email), l => l.SentAt, limit);

    public Task<IReadOnlyList<Notification>> GetNewestNotificationsAsync(Guid userId, int limit) =>
        NewestAsync(_context.Notifications.AsNoTracking().Where(n => n.UserId == userId), n => n.CreatedOn, limit);

    public Task<IReadOnlyList<AuditLog>> GetNewestAuditLogsAsync(Guid userId, int limit) =>
        NewestAsync(_context.AuditLogs.AsNoTracking().Where(l => l.EntityId == userId), l => l.CreatedAt, limit);

    public async Task<AuditLog?> GetLatestAuditAsync(Guid userId, IReadOnlyCollection<string> actions) =>
        await _context.AuditLogs.AsNoTracking()
            .Where(l => l.EntityId == userId && actions.Contains(l.Action))
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return await _context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetReferralCodesAsync(IEnumerable<Guid> referralCodeIds)
    {
        var ids = referralCodeIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return await _context.ReferralCodes.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Code);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetReferralRewardsAsync(IEnumerable<Guid> redemptionIds)
    {
        var ids = redemptionIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, decimal>();
        return await _context.AccountCredits.AsNoTracking()
            .Where(c => c.ReferralRedemptionId != null && ids.Contains(c.ReferralRedemptionId.Value))
            .ToDictionaryAsync(c => c.ReferralRedemptionId!.Value, c => c.Amount);
    }

    /// <summary>
    /// The rows whose key is at or above the <paramref name="limit"/>-th newest key. Cutting at a timestamp
    /// rather than a row count keeps every row tied with the last one, so the caller's merged order (time, then
    /// event id) is the same whatever window a page asks for: no duplicates or gaps across pages.
    /// </summary>
    private static async Task<IReadOnlyList<T>> NewestAsync<T>(IQueryable<T> query, Expression<Func<T, DateTime>> key, int limit)
    {
        var edge = await query.OrderByDescending(key).Select(key).Skip(Math.Max(0, limit - 1)).Take(1).ToListAsync();
        if (edge.Count == 0) return await query.ToListAsync();

        var cutoff = new Cutoff { Value = edge[0] };
        var atOrAfter = Expression.Lambda<Func<T, bool>>(
            Expression.GreaterThanOrEqual(key.Body, Expression.Property(Expression.Constant(cutoff), nameof(Cutoff.Value))),
            key.Parameters);
        return await query.Where(atOrAfter).ToListAsync();
    }

    // Captured by reference so EF sends the cutoff as a parameter.
    private sealed class Cutoff
    {
        public DateTime Value { get; init; }
    }
}
