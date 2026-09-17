namespace SaintHenriBasketball.Application.DTOs.SignupFunnel;

/// How far the players who signed up in a period got: confirmed their email, reserved a session,
/// paid, played. Every stage counts the same group of players, so the drop between two stages is
/// the number who stopped there.
public class SignupFunnelDto
{
    /// UTC instants, both inclusive of the days they fall in (Montreal days).
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    public List<FunnelStageDto> Stages { get; set; } = new();

    /// One row per month in the period, oldest first, so a trend is visible.
    public List<FunnelCohortDto> ByMonth { get; set; } = new();

    /// Players who registered in the period and have since been deactivated.
    public int Deactivated { get; set; }

    /// Median days from signing up to playing a first session; null when nobody has played yet.
    public double? MedianDaysToFirstPlay { get; set; }
}

public class FunnelStageDto
{
    /// Stable English key: registered, confirmed, reserved, paid, played.
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
    /// Share of the players who registered, 0 to 1.
    public double ShareOfRegistered { get; set; }
    /// How many were lost since the stage before; 0 for the first stage.
    public int DroppedSincePrevious { get; set; }
}

public class FunnelCohortDto
{
    /// Month label, yyyy-MM, in Montreal time.
    public string Label { get; set; } = string.Empty;
    public int Registered { get; set; }
    public int Confirmed { get; set; }
    public int Reserved { get; set; }
    public int Paid { get; set; }
    public int Played { get; set; }
}
