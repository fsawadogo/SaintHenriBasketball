using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class PromoReferralReportRepository : IPromoReferralReportRepository
{
    private readonly ApplicationDbContext _context;

    public PromoReferralReportRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PromoCode>> GetPromoCodesAsync() =>
        await _context.PromoCodes.AsNoTracking().OrderBy(p => p.Code).ToListAsync();

    public async Task<IReadOnlyList<PromoPaymentStats>> GetPromoPaymentStatsAsync(DateTime? from, DateTime? to)
    {
        var payments = _context.Payments.AsNoTracking()
            .Where(p => p.PromoCodeId != null && (p.Status == PaymentStatus.Completed || p.Status == PaymentStatus.Refunded));
        if (from is DateTime start) payments = payments.Where(p => p.PaymentDate >= start);
        if (to is DateTime end) payments = payments.Where(p => p.PaymentDate <= end);

        var groups = await payments
            .GroupBy(p => new { p.PromoCodeId, p.Status })
            .Select(g => new
            {
                g.Key.PromoCodeId,
                g.Key.Status,
                Count = g.Count(),
                ListPrice = g.Sum(p => p.OriginalAmount ?? p.Amount),
                Discount = g.Sum(p => p.DiscountAmount),
                First = g.Min(p => p.PaymentDate),
                Last = g.Max(p => p.PaymentDate),
            })
            .ToListAsync();

        return groups.Select(g => new PromoPaymentStats(g.PromoCodeId!.Value, g.Status == PaymentStatus.Refunded,
            g.Count, g.ListPrice, g.Discount, g.First, g.Last)).ToList();
    }

    public async Task<IReadOnlyList<AccountCreditKindTotal>> GetCreditTotalsAsync(DateTime? from, DateTime? to)
    {
        var credits = _context.AccountCredits.AsNoTracking();
        if (from is DateTime start) credits = credits.Where(c => c.CreatedAt >= start);
        if (to is DateTime end) credits = credits.Where(c => c.CreatedAt <= end);

        var grants = await credits.Where(c => c.Amount > 0)
            .GroupBy(c => c.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count(), Total = g.Sum(c => c.Amount) })
            .ToListAsync();
        var debits = await credits.Where(c => c.Amount < 0)
            .GroupBy(c => c.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count(), Total = g.Sum(c => c.Amount) })
            .ToListAsync();

        return grants.Select(g => new AccountCreditKindTotal(g.Kind, true, g.Count, g.Total))
            .Concat(debits.Select(d => new AccountCreditKindTotal(d.Kind, false, d.Count, d.Total)))
            .ToList();
    }

    public async Task<AccountCreditOutstanding> GetOutstandingCreditAsync(DateTime? asOf)
    {
        var credits = _context.AccountCredits.AsNoTracking();
        if (asOf is DateTime end) credits = credits.Where(c => c.CreatedAt <= end);

        var balances = await credits
            .GroupBy(c => c.UserId)
            .Select(g => g.Sum(c => c.Amount))
            .Where(balance => balance > 0)
            .ToListAsync();
        return new AccountCreditOutstanding(balances.Sum(), balances.Count);
    }
}
