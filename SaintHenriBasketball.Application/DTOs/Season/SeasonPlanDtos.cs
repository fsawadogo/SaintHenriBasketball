using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Season;

/// What a player needs to decide how to pay for the season that is open now.
public class SeasonPlanStateDto
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// The season pass price.
    public decimal Price { get; set; }

    public int Capacity { get; set; }
    /// Distinct players on the season plan: paid, chosen, or switched by an admin.
    public int SpotsTaken { get; set; }
    public int SpotsLeft { get; set; }
    public bool SoldOut { get; set; }
    /// Of SpotsTaken, how many are paid for.
    public int SpotsPaid { get; set; }
    /// Of SpotsTaken, how many are held by someone who has not paid yet.
    public int SpotsAwaitingPayment { get; set; }

    /// The plan this player chose for THIS season, or null if they have not chosen yet.
    /// Null is what makes the prompt appear; either value silences it.
    public PaymentPlan? MyPlan { get; set; }
    /// True when this player already holds a completed season payment for this season.
    public bool MyPlanPaid { get; set; }
}

public class ChooseSeasonPlanDto
{
    public PaymentPlan Plan { get; set; }
}

/// What an admin reset would do, or did. Paid passes are never counted in Cleared.
public class SeasonPlanResetDto
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    /// Choices without a completed payment — the ones a reset clears.
    public int Cleared { get; set; }
    /// Players holding a paid pass, left untouched.
    public int KeptPaid { get; set; }
}
