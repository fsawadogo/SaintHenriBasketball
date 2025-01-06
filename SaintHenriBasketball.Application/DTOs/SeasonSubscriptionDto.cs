using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs;

public class SeasonSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; }
    public string UserName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public bool IsActive { get; set; }
}