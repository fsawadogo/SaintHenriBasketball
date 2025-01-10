using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Payment;

public class CreatePaymentDto
{
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public PaymentPlan Plan { get; set; }
}