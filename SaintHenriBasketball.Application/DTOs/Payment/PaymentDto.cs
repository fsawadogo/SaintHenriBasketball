using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Payment;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public PaymentPlan Plan { get; set; }
    public Guid? SeasonId { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime PaymentDate { get; set; }
    public required string UserName { get; set; }
    public required string? UserEmail { get; set; }
    public string? Reference { get; set; }
    public Guid? SessionId { get; set; }
    public DateTime? SessionDate { get; set; }
    public decimal? OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal CreditApplied { get; set; }
    public string? PromoCode { get; set; }
}
