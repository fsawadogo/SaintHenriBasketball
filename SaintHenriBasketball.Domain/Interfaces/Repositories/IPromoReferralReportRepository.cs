using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// Payment totals for one promo code and one payment status (Completed or Refunded) in the range.
public record PromoPaymentStats(
    Guid PromoCodeId,
    bool Refunded,
    int Count,
    decimal ListPrice,
    decimal Discount,
    DateTime FirstPaymentDate,
    DateTime LastPaymentDate);

/// Ledger rows of one kind and sign (grants positive, debits negative) in the range.
public record AccountCreditKindTotal(AccountCreditKind Kind, bool Positive, int Count, decimal Total);

public record AccountCreditOutstanding(decimal Balance, int PlayersWithBalance);

/// Read-only aggregates for the promo, referral and credit reports. From and To are inclusive UTC instants.
public interface IPromoReferralReportRepository
{
    Task<IReadOnlyList<PromoCode>> GetPromoCodesAsync();

    /// Completed and Refunded payments that used a promo code, grouped by code and status, by PaymentDate.
    Task<IReadOnlyList<PromoPaymentStats>> GetPromoPaymentStatsAsync(DateTime? from, DateTime? to);

    /// Ledger rows by CreatedAt, grouped by kind and sign.
    Task<IReadOnlyList<AccountCreditKindTotal>> GetCreditTotalsAsync(DateTime? from, DateTime? to);

    /// Sum of positive player balances, counting ledger rows created up to <paramref name="asOf"/> (all rows when null).
    Task<AccountCreditOutstanding> GetOutstandingCreditAsync(DateTime? asOf);
}
