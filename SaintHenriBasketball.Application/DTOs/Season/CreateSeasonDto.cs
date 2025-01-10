using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Season;

public class CreateSeasonDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public SeasonStatus Status { get; set; } = SeasonStatus.Open;
    public string? Notes { get; set; }
}
