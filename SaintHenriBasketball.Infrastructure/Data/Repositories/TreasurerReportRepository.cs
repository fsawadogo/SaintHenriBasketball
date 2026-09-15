using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class TreasurerReportRepository : ITreasurerReportRepository
{
    // The payment-method rule (see TreasurerPaymentMethod): StripeService stores the Checkout Session id ("cs_...")
    // as the reference; PaymentService.ConfirmInteracPaymentAsync appends "|INTERAC:<bank reference>".
    private const string CardReferencePrefix = "cs_";
    private const string InteracReferenceMarker = "|INTERAC:";

    private readonly ApplicationDbContext _context;

    public TreasurerReportRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// Payment columns the report reads, with the method and refund date worked out in SQL.
    private sealed class PaymentFact
    {
        public Guid Id { get; set; }
        public PaymentStatus Status { get; set; }
        public PaymentPlan Plan { get; set; }
        public Guid? SeasonId { get; set; }
        public TreasurerPaymentMethod Method { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime RefundDate { get; set; }
        public RefundMethod? RefundMethod { get; set; }
        public DateTime? RefundedOn { get; set; }
        public decimal Amount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal CreditApplied { get; set; }
        public string? Reference { get; set; }
    }

    private static readonly Expression<Func<Payment, PaymentFact>> ToFact = p => new PaymentFact
    {
        Id = p.Id,
        Status = p.Status,
        Plan = p.Plan,
        SeasonId = p.SeasonId,
        // Card is checked first; the two flows refuse each other, so a reference never carries both markers.
        Method = p.Reference != null && p.Reference.StartsWith(CardReferencePrefix) ? TreasurerPaymentMethod.Card
            : p.Reference != null && p.Reference.Contains(InteracReferenceMarker) ? TreasurerPaymentMethod.Interac
            : TreasurerPaymentMethod.Other,
        PaymentDate = p.PaymentDate,
        RefundDate = p.RefundedOn ?? p.PaymentDate,
        RefundMethod = p.RefundMethod,
        RefundedOn = p.RefundedOn,
        Amount = p.Amount,
        DiscountAmount = p.DiscountAmount,
        CreditApplied = p.CreditApplied,
        Reference = p.Reference,
    };

    private IQueryable<PaymentFact> Facts() => _context.Payments.AsNoTracking().Select(ToFact);

    public async Task<IReadOnlyList<TreasurerReceiptBucket>> GetReceiptBucketsAsync(TreasurerReportFilter filter)
    {
        var query = Facts().Where(f => f.Status == PaymentStatus.Completed || f.Status == PaymentStatus.Pending || f.Status == PaymentStatus.Refunded);
        if (filter.SeasonId is Guid seasonId)
            query = query.Where(f => f.SeasonId == seasonId);
        else
            query = query.Where(f => f.PaymentDate >= filter.From!.Value && f.PaymentDate <= filter.To!.Value);

        var groups = await query
            .GroupBy(f => new { f.Status, f.Plan, f.SeasonId, f.Method, f.PaymentDate.Year, f.PaymentDate.Month, f.PaymentDate.Day, f.PaymentDate.Hour })
            .Select(g => new
            {
                g.Key,
                Count = g.Count(),
                Amount = g.Sum(f => f.Amount),
                DiscountAmount = g.Sum(f => f.DiscountAmount),
                DiscountedCount = g.Count(f => f.DiscountAmount > 0),
                CreditApplied = g.Sum(f => f.CreditApplied),
                CreditCount = g.Count(f => f.CreditApplied > 0),
            })
            .ToListAsync();

        return groups.Select(g => new TreasurerReceiptBucket(
                g.Key.Status, g.Key.Plan, g.Key.SeasonId, g.Key.Method,
                new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0, DateTimeKind.Utc),
                g.Count, g.Amount, g.DiscountAmount, g.DiscountedCount, g.CreditApplied, g.CreditCount))
            .ToList();
    }

    public async Task<IReadOnlyList<TreasurerRefundBucket>> GetRefundBucketsAsync(TreasurerReportFilter filter)
    {
        var query = Facts().Where(f => f.Status == PaymentStatus.Refunded);
        if (filter.SeasonId is Guid seasonId)
            query = query.Where(f => f.SeasonId == seasonId);
        else
            query = query.Where(f => f.RefundDate >= filter.From!.Value && f.RefundDate <= filter.To!.Value);

        var groups = await query
            .GroupBy(f => new { f.RefundMethod, f.Plan, f.SeasonId, f.RefundDate.Year, f.RefundDate.Month, f.RefundDate.Day, f.RefundDate.Hour })
            .Select(g => new { g.Key, Count = g.Count(), Amount = g.Sum(f => f.Amount) })
            .ToListAsync();

        return groups.Select(g => new TreasurerRefundBucket(
                g.Key.RefundMethod, g.Key.Plan, g.Key.SeasonId,
                new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0, DateTimeKind.Utc),
                g.Count, g.Amount))
            .ToList();
    }

    public async Task<IReadOnlyList<TreasurerPaymentRow>> GetPaymentRowsAsync(TreasurerReportFilter filter)
    {
        var query = _context.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed || p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Refunded);
        if (filter.SeasonId is Guid seasonId)
        {
            query = query.Where(p => p.SeasonId == seasonId);
        }
        else
        {
            var from = filter.From!.Value;
            var to = filter.To!.Value;
            // The same payments the two bucket queries count: received in the range, or refunded in it.
            query = query.Where(p => (p.PaymentDate >= from && p.PaymentDate <= to)
                || (p.Status == PaymentStatus.Refunded && (p.RefundedOn ?? p.PaymentDate) >= from && (p.RefundedOn ?? p.PaymentDate) <= to));
        }

        var rows = await query
            .OrderBy(p => p.PaymentDate).ThenBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.PaymentDate,
                p.User.FirstName,
                p.User.LastName,
                p.User.Email,
                p.Plan,
                p.SeasonId,
                SeasonName = p.Season != null ? p.Season.Name : null,
                p.Status,
                // Same rule as ToFact above.
                Method = p.Reference != null && p.Reference.StartsWith(CardReferencePrefix) ? TreasurerPaymentMethod.Card
                    : p.Reference != null && p.Reference.Contains(InteracReferenceMarker) ? TreasurerPaymentMethod.Interac
                    : TreasurerPaymentMethod.Other,
                p.Amount,
                p.DiscountAmount,
                p.CreditApplied,
                p.RefundMethod,
                p.RefundedOn,
                p.Reference,
            })
            .ToListAsync();

        return rows.Select(r => new TreasurerPaymentRow(
                r.Id, DateTime.SpecifyKind(r.PaymentDate, DateTimeKind.Utc), r.FirstName, r.LastName, r.Email, r.Plan, r.SeasonId, r.SeasonName,
                r.Status, r.Method, r.Amount, r.DiscountAmount, r.CreditApplied, r.RefundMethod,
                r.RefundedOn is DateTime refunded ? DateTime.SpecifyKind(refunded, DateTimeKind.Utc) : null, r.Reference))
            .ToList();
    }

    public async Task<IReadOnlyList<TreasurerSeasonInfo>> GetSeasonsAsync(IReadOnlyCollection<Guid> seasonIds)
    {
        if (seasonIds.Count == 0) return Array.Empty<TreasurerSeasonInfo>();
        var ids = seasonIds.ToList();
        var seasons = await _context.Seasons.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.StartDate })
            .ToListAsync();
        return seasons.Select(s => new TreasurerSeasonInfo(s.Id, s.Name, s.StartDate)).ToList();
    }
}
