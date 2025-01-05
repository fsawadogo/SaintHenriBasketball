using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs;

public class UpdatePaymentStatusDto
{
    public PaymentStatus Status { get; set; }
}