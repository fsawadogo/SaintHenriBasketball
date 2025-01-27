using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class SeasonSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime CreatedOn { get; set; }
    public bool IsActive { get; set; }
    public virtual required ApplicationUser User { get; set; }
}