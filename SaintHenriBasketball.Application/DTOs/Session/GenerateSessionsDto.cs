namespace SaintHenriBasketball.Application.DTOs.Session;

public class GenerateSessionsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int MaxCapacity { get; set; } = 20;
    public decimal DropInPrice { get; set; } = 10;
    public string StartTime { get; set; } = "10:00";
    public string EndTime { get; set; } = "12:00";
    public string Location { get; set; } = "717 Saint-Ferdinand Street, Montreal, QC H4C 3L7";
}

public class GenerateSessionsResponseDto
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<DateTime> CreatedDates { get; set; } = new();
    public List<DateTime> SkippedDates { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
