namespace SaintHenriBasketball.Application.DTOs.Payment;

public class DropInPaymentLinkDto
{
    public Guid PaymentId { get; set; }
    public Guid SessionId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentUrl { get; set; } = string.Empty;
    public string InteracEmail { get; set; } = "pay@sainthenribasketball.com";
    public DateTime ExpiresAt { get; set; }
}
