namespace SaintHenriBasketball.Application.DTOs.SeasonDashboard;

/// One season at a glance for an admin: passes sold, money in and owed, and how each session filled.
/// Every figure covers the chosen season only, so two seasons never mix.
public class SeasonDashboardDto
{
    public Guid SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }

    public SeasonPassesDto Passes { get; set; } = new();
    public SeasonMoneyDto Money { get; set; } = new();
    public SeasonAttendanceDto Attendance { get; set; } = new();
    public List<SeasonDashboardSessionDto> Sessions { get; set; } = new();

    /// Other seasons an admin can switch to, newest first.
    public List<SeasonOptionDto> Seasons { get; set; } = new();
}

public class SeasonOptionDto
{
    public Guid SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// A pass is held by a completed season payment; an unpaid choice holds nothing.
public class SeasonPassesDto
{
    public int Capacity { get; set; }
    public int Sold { get; set; }
    public int Left { get; set; }
    /// Players who chose the season plan but have not paid, so they hold no spot.
    public int Unpaid { get; set; }
}

public class SeasonMoneyDto
{
    /// Completed payments, minus nothing: what actually came in.
    public decimal Collected { get; set; }
    public decimal CollectedFromPasses { get; set; }
    public decimal CollectedFromDropIns { get; set; }
    /// Pending payments: money a player has committed to but the club has not confirmed.
    public decimal Pending { get; set; }
    public int PendingCount { get; set; }
    /// Days since the oldest pending payment was created; null when nothing is pending.
    public int? OldestPendingDays { get; set; }
    /// Pending drop-ins grouped by age, oldest first: "8-30 days" and so on.
    public List<AgingBucketDto> PendingByAge { get; set; } = new();
    public decimal Refunded { get; set; }
}

public class AgingBucketDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class SeasonAttendanceDto
{
    public int SessionsTotal { get; set; }
    public int SessionsPlayed { get; set; }
    public int ReservedTotal { get; set; }
    public int AttendedTotal { get; set; }
    /// Attended ÷ places offered on sessions already played, 0 to 1.
    public double FillRate { get; set; }
}

public class SeasonDashboardSessionDto
{
    public Guid SessionId { get; set; }
    public DateTime SessionDate { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Reserved { get; set; }
    public int Attended { get; set; }
    /// True once the session's day has passed, so the page can separate played from upcoming.
    public bool HasHappened { get; set; }
    public decimal DropInsCollected { get; set; }
    public int DropInsPending { get; set; }
}
