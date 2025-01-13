using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Season;

public class SeasonDto
{
    public Guid Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public SeasonStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedOn { get; set; }
    public int RegisteredUsersCount { get; set; }
    public List<SeasonUserDto> RegisteredUsers { get; set; } = new();
    public bool IsCurrentSeason { get; set; }
}
