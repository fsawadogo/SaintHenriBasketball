using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Payment;

public class RefundPaymentDto
{
    public RefundMethod Method { get; set; }
    public string? Reason { get; set; }
}
