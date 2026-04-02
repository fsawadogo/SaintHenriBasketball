namespace SaintHenriBasketball.Application.DTOs.Payment;

public class CreateDropInPaymentDto
{
    public Guid SessionId { get; set; }
    public int PaymentMethod { get; set; } // 0 = Interac, 1 = CreditCard, 2 = DebitCard
    public string? InteracReference { get; set; }
}
