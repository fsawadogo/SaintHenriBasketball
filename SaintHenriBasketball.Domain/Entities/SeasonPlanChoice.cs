using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

/// How one player chose to pay for one season.
///
/// The plan has always been a single global field on the user, which cannot answer "what did this
/// player choose for THIS season". This record does. `ApplicationUser.PaymentPlan` is kept as the
/// denormalised current value so every existing reader keeps working.
///
/// A row here is a *choice*, not a spot. A season pass spot is held by a completed payment; an
/// unpaid choice holds nothing and is what an admin reset clears.
public class SeasonPlanChoice
{
    public Guid Id { get; private set; }
    public Guid SeasonId { get; set; }
    public Guid UserId { get; set; }
    public PaymentPlan Plan { get; set; }
    public DateTime ChosenOn { get; set; }

    public virtual Season Season { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;

    private SeasonPlanChoice() { } // For EF Core

    public SeasonPlanChoice(Guid seasonId, Guid userId, PaymentPlan plan)
    {
        Id = Guid.NewGuid();
        SeasonId = seasonId;
        UserId = userId;
        Plan = plan;
        ChosenOn = DateTime.UtcNow;
    }
}
