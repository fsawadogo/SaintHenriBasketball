using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// <summary>
/// Target values for a Pending payment's promo discount and account credit.
/// <paramref name="ReservePromoCodeId"/> is set only when a new promo use must be reserved.
/// </summary>
public record PaymentAdjustment(
    decimal OriginalAmount,
    decimal DiscountAmount,
    decimal CreditApplied,
    Guid? PromoCodeId,
    Guid? ReservePromoCodeId,
    decimal Amount);

public enum PaymentAdjustmentResult
{
    Applied,
    /// The promo code was deactivated, expired or used up before the use could be reserved.
    PromoUnavailable,
    /// The payment row or the player's credit balance changed since it was read.
    PaymentChanged,
}

public interface IPaymentRepository
{
    Task<IReadOnlyList<Payment>> GetPaymentsByUserAsync(Guid userId);
    Task<Payment> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Payment>> GetBySessionAsync(Guid sessionId);
    Task<IReadOnlyList<Payment>> GetAllAsync();

    /// One page of payments matching <paramref name="criteria"/>, newest first, with totals for every match.
    Task<PaymentSearchPage> SearchAsync(PaymentSearchCriteria criteria);
    Task<IReadOnlyList<Payment>> GetPaymentsByStatusAsync(PaymentStatus status);
    Task<IReadOnlyList<Payment>> GetPaymentsByTypeAsync(PaymentPlan plan);
    Task<(Payment Payment, bool Created)> GetOrCreateSessionPaymentAsync(Guid userId, Guid sessionId, decimal amount);
    Task<(Payment Payment, bool Created)> GetOrCreateSeasonPaymentAsync(Guid userId, Guid seasonId, decimal amount);
    Task AddAsync(Payment payment);
    Task UpdateAsync(Payment payment);
    Task<bool> TrySetPendingReferenceAsync(Guid id, string? expectedReference, string reference);
    Task<IEnumerable<Payment>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// Returns the most recent non-Refunded payment for the (user, session) pair, or null.
    /// Used by the auto-billing path to short-circuit when a Pending or Completed payment
    /// already exists.
    Task<Payment?> GetByUserAndSessionAsync(Guid userId, Guid sessionId);

    /// True when any payment exists for the (user, session) pair, whatever its status — a refunded one included.
    /// The unattended billing sweep uses this so a refund isn't undone by the next hourly run; the QR check-in
    /// path keeps using <see cref="GetByUserAndSessionAsync"/>, which lets a refunded player pay again.
    Task<bool> HasAnyPaymentForSessionAsync(Guid userId, Guid sessionId);

    /// The payment <see cref="GetOrCreateSeasonPaymentAsync"/> would reuse (neither Refunded nor Failed), or null.
    Task<Payment?> GetByUserAndSeasonAsync(Guid userId, Guid seasonId);

    /// True when a payment already carries this reference, ignoring any submitted Interac suffix.
    Task<bool> ReferenceExistsAsync(string reference);

    Task<bool> HasCompletedPaymentAsync(Guid userId);

    /// True when the user has a Completed payment with a positive amount other than
    /// <paramref name="paymentId"/> that was completed before <paramref name="completedBefore"/>.
    Task<bool> HasEarlierPaidPaymentAsync(Guid userId, Guid paymentId, DateTime completedBefore);

    /// <summary>
    /// In one transaction: reserves a promo use (when requested), debits the newly applied credit
    /// from the ledger, and compare-and-sets the payment (Pending, same Reference, Amount and
    /// adjustments as <paramref name="payment"/> currently holds). Nothing is written unless all
    /// three succeed. On success the tracked <paramref name="payment"/> is updated in place.
    /// </summary>
    Task<PaymentAdjustmentResult> TryApplyAdjustmentsAsync(Payment payment, PaymentAdjustment adjustment);
}
