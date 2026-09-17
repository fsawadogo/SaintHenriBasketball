using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Season;

public class CreateSeasonDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public SeasonStatus Status { get; set; } = SeasonStatus.Open;
    public string? Notes { get; set; }
    /// How many players may hold a season pass. Defaults to 15 when omitted.
    public int SeasonPassCapacity { get; set; } = Domain.Entities.Season.DefaultSeasonPassCapacity;
}
