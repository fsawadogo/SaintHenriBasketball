using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Payment;

public class UpdatePaymentDto
{
    public decimal Amount { get; set; }
    public PaymentPlan Plan { get; set; }
    public PaymentStatus Status { get; set; }   
}