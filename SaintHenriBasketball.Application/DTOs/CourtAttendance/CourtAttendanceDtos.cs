namespace SaintHenriBasketball.Application.DTOs.CourtAttendance;

/// Payment status shown on the roster. Drop-in players show their session payment's status
/// (Pending, Completed, Failed) or NotBilled; season players are covered by a paid season pass or not.
public static class RosterPaymentStatus
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string NotBilled = "NotBilled";
    public const string CoveredBySeasonPass = "CoveredBySeasonPass";
    public const string SeasonFeeUnpaid = "SeasonFeeUnpaid";
}

public class SessionRosterDto
{
    public SessionRosterHeaderDto Session { get; set; } = new();
    public IReadOnlyList<RosterPlayerDto> Players { get; set; } = Array.Empty<RosterPlayerDto>();
}

public class SessionRosterHeaderDto
{
    public Guid SessionId { get; set; }
    /// Montreal calendar date, "yyyy-MM-dd".
    public string SessionDate { get; set; } = string.Empty;
    /// Montreal local times as stored on the session ("HH:mm").
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    /// The same start and end as UTC instants.
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public int RegisteredPlayersCount { get; set; }
    public RosterOutcomeCountsDto Counts { get; set; } = new();
}

public class RosterOutcomeCountsDto
{
    public int Total { get; set; }
    public int Unmarked { get; set; }
    public int Attended { get; set; }
    public int NoShow { get; set; }
    public int WalkIn { get; set; }
}

public class RosterPlayerDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsDeactivated { get; set; }
    /// "DropIn" or "Season" (the player's current plan, as billing uses it).
    public string PaymentPlan { get; set; } = string.Empty;
    /// The player's own answer; null when they never answered.
    public bool? IsAttending { get; set; }
    /// "Unmarked", "Attended", "NoShow" or "WalkIn".
    public string Outcome { get; set; } = string.Empty;
    /// UTC.
    public DateTime? CheckInTime { get; set; }
    /// One of <see cref="RosterPaymentStatus"/>.
    public string PaymentStatus { get; set; } = string.Empty;
    /// The drop-in session payment or the paid season payment behind PaymentStatus, when there is one.
    public Guid? PaymentId { get; set; }
}

public class SetAttendanceOutcomeRequest
{
    /// "Attended", "NoShow" or "Unmarked" (case-insensitive).
    public string? Outcome { get; set; }
}

public class AddWalkInRequest
{
    public Guid UserId { get; set; }
}

public class NoShowStatsQuery
{
    /// Inclusive Montreal calendar dates. Defaults: To = today, From = To minus 90 days.
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? UserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class NoShowStatsPageDto
{
    public IReadOnlyList<NoShowStatsItemDto> Items { get; set; } = Array.Empty<NoShowStatsItemDto>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    /// The window actually used, "yyyy-MM-dd".
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public class NoShowStatsItemDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsDeactivated { get; set; }
    /// Past, non-cancelled sessions the player was on the roster for: Attended + NoShows + Unmarked.
    public int SessionsRegistered { get; set; }
    /// Includes walk-ins.
    public int Attended { get; set; }
    public int NoShows { get; set; }
    public int Unmarked { get; set; }
    /// Percent (0-100, one decimal) of marked sessions that were no-shows; 0 when nothing was marked.
    public double NoShowRate { get; set; }
}
