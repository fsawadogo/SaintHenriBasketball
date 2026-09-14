namespace SaintHenriBasketball.Application.DTOs.Payment;

public class CreateSeasonPaymentDto
{
    public Guid SeasonId { get; set; }
    public int PaymentMethod { get; set; }
    public string? InteracReference { get; set; }
    public string? PromoCode { get; set; }
}
