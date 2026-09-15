using SaintHenriBasketball.Application.DTOs.PromoReferralReports;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class PromoReferralReportService : IPromoReferralReportService
{
    public const string DateRangeMessage = "The start date must be on or before the end date.";

    public const string UsageNote =
        "Times used counts completed payments with the code. The code's own counter goes up when a checkout reserves the code, "
        + "before the payment is confirmed, and never goes down: it also includes payments still pending, payments that failed "
        + "or were voided, and refunded payments. Refunded payments are not counted as uses here.";

    private readonly IPromoReferralReportRepository _repository;

    public PromoReferralReportService(IPromoReferralReportRepository repository)
    {
        _repository = repository;
    }

    public async Task<PromoUsageReportDto> GetPromoUsageAsync(ReportRangeQuery range)
    {
        var (from, to) = NormalizeRange(range);
        var now = DateTime.UtcNow;
        var codes = await _repository.GetPromoCodesAsync();
        var stats = await _repository.GetPromoPaymentStatsAsync(from, to);

        var rows = codes.Select(code =>
        {
            var completed = stats.FirstOrDefault(s => s.PromoCodeId == code.Id && !s.Refunded);
            var refunded = stats.FirstOrDefault(s => s.PromoCodeId == code.Id && s.Refunded);
            return new PromoUsageRowDto
            {
                PromoCodeId = code.Id,
                Code = code.Code,
                DiscountType = code.DiscountType,
                DiscountValue = code.DiscountValue,
                AppliesTo = code.AppliesTo,
                IsActive = code.IsActive,
                ValidFrom = Utc(code.ValidFrom),
                ValidUntil = Utc(code.ValidUntil),
                IsExpired = code.ValidUntil < now,
                MaxUses = code.MaxUses,
                TimesUsed = completed?.Count ?? 0,
                RefundedUses = refunded?.Count ?? 0,
                TimesUsedCounter = code.TimesUsed,
                TotalListPrice = completed?.ListPrice ?? 0m,
                TotalDiscount = completed?.Discount ?? 0m,
                RevenueAfterDiscount = completed is null ? 0m : completed.ListPrice - completed.Discount,
                FirstUsedAt = completed is null ? null : Utc(completed.FirstPaymentDate),
                LastUsedAt = completed is null ? null : Utc(completed.LastPaymentDate),
            };
        })
            .OrderByDescending(r => r.TotalDiscount)
            .ThenByDescending(r => r.TimesUsed)
            .ThenBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PromoUsageReportDto
        {
            From = from,
            To = to,
            GeneratedAt = now,
            UsageNote = UsageNote,
            Items = rows,
            Totals = new PromoUsageTotalsDto
            {
                PromoCodes = rows.Count,
                CodesUsed = rows.Count(r => r.TimesUsed > 0),
                TimesUsed = rows.Sum(r => r.TimesUsed),
                RefundedUses = rows.Sum(r => r.RefundedUses),
                TotalListPrice = rows.Sum(r => r.TotalListPrice),
                TotalDiscount = rows.Sum(r => r.TotalDiscount),
                RevenueAfterDiscount = rows.Sum(r => r.RevenueAfterDiscount),
            },
        };
    }

    public async Task<CreditsReportDto> GetCreditsReportAsync(ReportRangeQuery range)
    {
        var (from, to) = NormalizeRange(range);
        var now = DateTime.UtcNow;
        var totals = await _repository.GetCreditTotalsAsync(from, to);
        var outstanding = await _repository.GetOutstandingCreditAsync(to);

        CreditAmountDto Amount(AccountCreditKind kind, bool positive)
        {
            var match = totals.FirstOrDefault(t => t.Kind == kind && t.Positive == positive);
            return new CreditAmountDto { Count = match?.Count ?? 0, Total = Math.Abs(match?.Total ?? 0m) };
        }

        var granted = totals.Where(t => t.Positive)
            .OrderBy(t => t.Kind)
            .Select(t => new CreditKindTotalDto { Kind = t.Kind, KindName = t.Kind.ToString(), Count = t.Count, Total = t.Total })
            .ToList();
        var added = Amount(AccountCreditKind.ManualAdjustment, positive: true);
        var removed = Amount(AccountCreditKind.ManualAdjustment, positive: false);

        return new CreditsReportDto
        {
            From = from,
            To = to,
            GeneratedAt = now,
            Granted = granted,
            GrantedTotal = granted.Sum(g => g.Total),
            Spent = Amount(AccountCreditKind.AppliedToPayment, positive: false),
            RefundsAsCredit = Amount(AccountCreditKind.Refund, positive: true),
            ManualAdjustments = new ManualAdjustmentsDto { Added = added, Removed = removed, Net = added.Total - removed.Total },
            Outstanding = new OutstandingCreditDto
            {
                AsOf = to ?? now,
                Balance = outstanding.Balance,
                PlayersWithBalance = outstanding.PlayersWithBalance,
            },
        };
    }

    private static (DateTime? From, DateTime? To) NormalizeRange(ReportRangeQuery range)
    {
        var from = range.From is DateTime f ? ToUtc(f) : (DateTime?)null;
        var to = range.To is DateTime t ? ToUtc(t) : (DateTime?)null;
        if (from is DateTime start && to is DateTime end && start > end)
            throw new ValidationException(DateRangeMessage);
        return (from, to);
    }

    /// Query-string dates with an offset bind as local time; dates without one are taken as UTC.
    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
