using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Stats;

public class PersonalStatsDto
{
    public int TotalRegistered { get; set; }
    public int TotalAttended { get; set; }
    public double AttendanceRate { get; set; }
    public decimal TotalSpent { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DayOfWeek? FavoriteDayOfWeek { get; set; }
    public string? FavoriteStartTime { get; set; }
    public DateTime? FirstSessionOn { get; set; }
    public DateTime? LastSessionOn { get; set; }
    public List<MonthlyAttendanceDto> MonthlyBreakdown { get; set; } = new();
    public List<BadgeDto> Badges { get; set; } = new();
    public PaymentPlan CurrentPlan { get; set; }
}

public class MonthlyAttendanceDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Attended { get; set; }
    public int Registered { get; set; }
}

public class BadgeDto
{
    public string Key { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
    public string LabelFr { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public string DescriptionFr { get; set; } = string.Empty;
    public DateTime? EarnedOn { get; set; }
}
