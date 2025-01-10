using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Payment;

public class UpdatePaymentStatusDto
{
    public PaymentStatus Status { get; set; }
}