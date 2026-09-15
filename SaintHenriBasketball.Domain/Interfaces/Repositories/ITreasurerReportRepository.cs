using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// <summary>
/// How a payment was (or is being) paid, read from <c>Payment.Reference</c>. There is no method column, so the
/// reference the payment flows write is the only record of it:
/// <list type="bullet">
/// <item><b>Card</b>: the reference starts with <c>cs_</c>. StripeService.CreateAsync (the drop-in and season
/// checkout endpoints) overwrites the reference with the Stripe Checkout Session id, and the webhook completes
/// the payment. PaymentRefundService uses the same test to allow card refunds.</item>
/// <item><b>Interac</b>: the reference contains <c>|INTERAC:</c>. PaymentService.ConfirmInteracPaymentAsync appends
/// the player's bank confirmation number after that marker, and reconciliation completes only those payments.</item>
/// <item><b>Other</b>: anything else. These are payments an admin created or completed by hand (DROPIN-/SEASON-
/// references), payments fully covered by a promo code or account credit, and legacy rows.</item>
/// </list>
/// The two flows exclude each other: Interac confirmation refuses a <c>cs_</c> payment and card checkout refuses
/// one with an Interac reference. Card is checked first.
/// </summary>
public enum TreasurerPaymentMethod
{
    Interac = 0,
    Card = 1,
    Other = 2,
}

/// <summary>
/// Scope of a treasurer report: either a UTC range (inclusive at both ends) or one season (every payment tied to
/// it, whatever its date).
/// </summary>
public record TreasurerReportFilter(DateTime? From, DateTime? To, Guid? SeasonId);

/// <summary>
/// Completed, Pending and Refunded payments summed per status, plan, season, method and UTC hour of PaymentDate.
/// Hour buckets map exactly onto local months, because Toronto's UTC offsets are whole hours.
/// </summary>
public record TreasurerReceiptBucket(
    PaymentStatus Status,
    PaymentPlan Plan,
    Guid? SeasonId,
    TreasurerPaymentMethod Method,
    DateTime HourUtc,
    int Count,
    decimal Amount,
    decimal DiscountAmount,
    int DiscountedCount,
    decimal CreditApplied,
    int CreditCount);

/// <summary>
/// Refunded payments summed per refund method, plan, season and UTC hour of the refund. The refund date is
/// RefundedOn, or PaymentDate when a payment was marked refunded without the refund flow.
/// </summary>
public record TreasurerRefundBucket(
    RefundMethod? RefundMethod,
    PaymentPlan Plan,
    Guid? SeasonId,
    DateTime HourUtc,
    int Count,
    decimal Amount);

/// <summary>One payment in the CSV detail section.</summary>
public record TreasurerPaymentRow(
    Guid Id,
    DateTime PaymentDate,
    string? FirstName,
    string? LastName,
    string? Email,
    PaymentPlan Plan,
    Guid? SeasonId,
    string? SeasonName,
    PaymentStatus Status,
    TreasurerPaymentMethod Method,
    decimal Amount,
    decimal DiscountAmount,
    decimal CreditApplied,
    RefundMethod? RefundMethod,
    DateTime? RefundedOn,
    string? Reference);

public record TreasurerSeasonInfo(Guid Id, string Name, DateTime StartDate);

/// Read-only queries for the treasurer report. Sums run in SQL.
public interface ITreasurerReportRepository
{
    /// Completed, Pending and Refunded payments whose PaymentDate is in the range (or that belong to the season).
    Task<IReadOnlyList<TreasurerReceiptBucket>> GetReceiptBucketsAsync(TreasurerReportFilter filter);

    /// Refunded payments whose refund date is in the range (or that belong to the season).
    Task<IReadOnlyList<TreasurerRefundBucket>> GetRefundBucketsAsync(TreasurerReportFilter filter);

    /// Every payment either query above counts, oldest first.
    Task<IReadOnlyList<TreasurerPaymentRow>> GetPaymentRowsAsync(TreasurerReportFilter filter);

    Task<IReadOnlyList<TreasurerSeasonInfo>> GetSeasonsAsync(IReadOnlyCollection<Guid> seasonIds);
}
