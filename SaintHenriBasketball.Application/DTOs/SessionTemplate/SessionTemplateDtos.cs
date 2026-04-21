namespace SaintHenriBasketball.Application.DTOs.SessionTemplate;

public class SessionTemplateDto
{
    public Guid Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class UpsertSessionTemplateDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public bool IsActive { get; set; } = true;
}

public class GenerateSessionsRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class GenerateSessionsResultDto
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<DateTime> CreatedDates { get; set; } = new();
    public List<DateTime> SkippedDates { get; set; } = new();
}
