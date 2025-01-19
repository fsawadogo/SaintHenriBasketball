namespace SaintHenriBasketball.Application.DTOs.Session;

public class CreateSessionDto
{
    public DateTime SessionDate { get; set; }
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public required string StartTime { get; set; }
    public required string EndTime { get; set; }
    public required string Location { get; set; }
}
