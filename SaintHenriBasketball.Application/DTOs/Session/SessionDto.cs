using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Session;

public class SessionDto
{
    public Guid Id { get; set; }
    public DateTime SessionDate { get; set; }
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public SessionStatus Status { get; set; }
    public int RegisteredPlayersCount { get; set; }
    public required string StartTime { get; set; }
    public required string EndTime { get; set; }
    public required string Location { get; set; }
}