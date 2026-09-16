namespace SaintHenriBasketball.Application.DTOs.PublicSchedule;

public class PublicSessionDto
{
    public Guid Id { get; set; }
    public DateTime SessionDate { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int MaxCapacity { get; set; }
    public int RegisteredPlayersCount { get; set; }
    public int SpotsRemaining { get; set; }
    public decimal DropInPrice { get; set; }

    /// True when the session has no spots left. Shown as a "Full" badge instead of hiding the session.
    public bool IsFull { get; set; }
}
