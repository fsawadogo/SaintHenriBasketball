namespace SaintHenriBasketball.Application.DTOs.Email;

/// What happened to one player's money when their session was cancelled.
public enum CancellationMoney
{
    /// They had not paid and owed nothing.
    Nothing,
    /// A payment they had not yet settled was cancelled, so there is nothing to pay.
    Cancelled,
    /// Money they had paid came back as account credit.
    Credited,
    /// They paid, and the club has not given it back. The email must say so rather than imply otherwise.
    StillHeld,
    /// Their season pass covers every session, so a cancelled one costs them nothing.
    CoveredByPass,
}

/// Everything one cancellation email says. Built per player, because the money part differs per player.
public class SessionCancellationEmailModel
{
    public string FirstName { get; set; } = string.Empty;

    public DateTime SessionDate { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string? EndTime { get; set; }
    public string? Location { get; set; }
    public string? Reason { get; set; }

    /// True for someone who was waiting for a place rather than holding one.
    public bool WasWaiting { get; set; }

    public CancellationMoney Money { get; set; }
    public decimal Amount { get; set; }

    /// The next session that is still going ahead, when there is one.
    public Guid? NextSessionId { get; set; }
    public DateTime? NextSessionDate { get; set; }
    public string? NextStartTime { get; set; }
    public string? NextEndTime { get; set; }
    public int? NextSpotsLeft { get; set; }

    /// Where the app lives, for the links.
    public string AppUrl { get; set; } = "https://sainthenribasketball.com";
}
