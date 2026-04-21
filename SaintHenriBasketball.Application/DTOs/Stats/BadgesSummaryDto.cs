namespace SaintHenriBasketball.Application.DTOs.Stats;

public class BadgesSummaryDto
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalAttended { get; set; }
    public List<BadgeDto> Badges { get; set; } = new();
}
