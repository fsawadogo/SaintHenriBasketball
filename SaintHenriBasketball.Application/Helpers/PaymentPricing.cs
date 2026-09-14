using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Helpers;

/// Pure pricing rules shared by the payment quote, payment creation and promo validation.
public static class PaymentPricing
{
    public const string PromoCodesUnavailableMessage = "Promo codes are not available.";
    public const string PaymentLockedMessage = "This payment has already been started; a promo code can no longer be applied.";
    public const string PromoAlreadyAppliedMessage = "A promo code is already applied to this payment.";
    public const string PromoUsageLimitMessage = "This promo code has reached its usage limit.";
    public const string PaymentChangedMessage = "The payment changed; please try again.";

    /// Stripe rejects CAD card charges below 50 cents.
    public const decimal MinimumCardAmount = 0.50m;
    public const string BelowCardMinimumMessage = "Card payments must be at least $0.50. Pay the remaining amount by Interac e-Transfer.";

    public static bool IsBelowCardMinimum(decimal total) => total > 0m && total < MinimumCardAmount;

    public static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    public static decimal RoundCents(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// A payment whose price the player may no longer change: an Interac reference was submitted,
    /// a Stripe checkout was opened (the reference holds the cs_ session id), or it left Pending.
    /// </summary>
    public static bool IsLocked(Payment payment) =>
        payment.Status != PaymentStatus.Pending
        || payment.Reference?.Contains("|INTERAC:", StringComparison.Ordinal) == true
        || payment.Reference?.StartsWith("cs_", StringComparison.Ordinal) == true;

    /// PaymentPlan and PromoAppliesTo number their members differently, so never cast between them.
    public static PromoAppliesTo ToPromoTarget(PaymentPlan plan) => plan switch
    {
        PaymentPlan.DropIn => PromoAppliesTo.DropIn,
        PaymentPlan.Season => PromoAppliesTo.Season,
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "Unsupported payment plan"),
    };

    /// Null when the promo can be used for this plan right now (usage is re-checked atomically when reserved).
    public static string? GetIneligibilityReason(PromoCode? promo, PaymentPlan plan, DateTime nowUtc)
    {
        if (promo is null) return "This promo code was not found.";
        if (!promo.IsActive) return "This promo code is no longer active.";
        if (nowUtc < promo.ValidFrom) return "This promo code is not valid yet.";
        if (nowUtc > promo.ValidUntil) return "This promo code has expired.";
        if (promo.MaxUses is int max && promo.TimesUsed >= max) return PromoUsageLimitMessage;
        if (promo.AppliesTo != PromoAppliesTo.Both && promo.AppliesTo != ToPromoTarget(plan))
            return "This promo code does not apply to this plan.";
        return null;
    }

    /// Discount in dollars, rounded to cents and never more than the price.
    public static decimal CalculateDiscount(PromoCode promo, decimal price)
    {
        if (price <= 0) return 0m;
        var discount = promo.DiscountType == PromoDiscountType.Percent
            ? RoundCents(price * promo.DiscountValue / 100m)
            : RoundCents(promo.DiscountValue);
        return Math.Clamp(discount, 0m, price);
    }
}
