using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Payment;

public class PaymentQuoteRequestDto
{
    public PaymentPlan Plan { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? SeasonId { get; set; }
    public string? PromoCode { get; set; }
}

public class PaymentQuoteDto
{
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal CreditApplied { get; set; }
    public decimal Total { get; set; }
    /// The promo code the total includes, or null.
    public string? PromoCode { get; set; }
    /// Why the requested promo code was not applied, or null.
    public string? PromoError { get; set; }
    /// The existing payment was already started; these are its stored values and cannot change.
    public bool Locked { get; set; }
}
