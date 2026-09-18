namespace SaintHenriBasketball.Application.DTOs.Email;

/// Everything the "choose your plan" email says, a week before a season starts.
/// Prices and places come from the season itself, so the email can never quote a stale number.
public class SeasonPlanChoiceEmailModel
{
    public string FirstName { get; set; } = string.Empty;

    public string SeasonName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// Whole days between today and the first session, for the headline.
    public int DaysUntilStart { get; set; }

    public decimal PassPrice { get; set; }
    /// Null when no session is scheduled yet to price a drop-in from.
    public decimal? DropInPrice { get; set; }

    public int PassCapacity { get; set; }
    public int PassesLeft { get; set; }

    /// The first session of the season, when there is one.
    public DateTime? FirstSessionDate { get; set; }
    public string? FirstSessionStart { get; set; }
    public string? FirstSessionEnd { get; set; }
    public string? Location { get; set; }
    public int SessionCount { get; set; }

    public string AppUrl { get; set; } = "https://sainthenribasketball.com";
}
