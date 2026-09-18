using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

/// <summary>
/// What a player is told right after picking how they will pay for a season.
///
/// It confirms the choice back to them and says what happens next, which differs sharply: a season
/// pass still has to be paid for before it is a spot, while pay-per-session needs nothing at all.
/// </summary>
public class PlanChoiceConfirmationEmailModel
{
    public string FirstName { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public PaymentPlan Plan { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// What the season pass costs.
    public decimal PassPrice { get; set; }
    /// What one session costs, when the season has a session to read it from.
    public decimal? DropInPrice { get; set; }
    /// True when this player has already paid for their pass, so the email must not ask for money.
    public bool AlreadyPaid { get; set; }
    /// Season passes still available after this choice.
    public int SpotsLeft { get; set; }
    public string AppUrl { get; set; } = string.Empty;
}
