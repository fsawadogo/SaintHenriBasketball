using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Session;

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
