using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs;
public class CreateSessionDto
{
    public DateTime SessionDate { get; set; }
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public string Location { get; set; }
}

public class UpdateSessionDto
{
    public DateTime? SessionDate { get; set; }
    public int? MaxCapacity { get; set; }
    public decimal? DropInPrice { get; set; }
    public SessionStatus Status { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Location { get; set; }
}

public class SessionDto
{
    public Guid Id { get; set; }
    public DateTime SessionDate { get; set; }
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public SessionStatus Status { get; set; }
    public int RegisteredPlayersCount { get; set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public string Location { get; set; }
}