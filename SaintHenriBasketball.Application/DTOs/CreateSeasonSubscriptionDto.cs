namespace SaintHenriBasketball.Application.DTOs;

public class CreateSeasonSubscriptionDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
}
