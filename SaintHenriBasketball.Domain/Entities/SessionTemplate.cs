namespace SaintHenriBasketball.Domain.Entities;

public class SessionTemplate
{
    public Guid Id { get; private set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string StartTime { get; set; } = "10:00";
    public string EndTime { get; set; } = "12:00";
    public string Location { get; set; } = "717 Saint-Ferdinand Street, Montreal, QC H4C 3L7";
    public int MaxCapacity { get; set; } = 20;
    public decimal DropInPrice { get; set; } = 10.00m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; private set; }

    private SessionTemplate() { }

    public SessionTemplate(DayOfWeek dayOfWeek, string startTime, string endTime, string location, int maxCapacity, decimal dropInPrice)
    {
        Id = Guid.NewGuid();
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        Location = location;
        MaxCapacity = maxCapacity;
        DropInPrice = dropInPrice;
        IsActive = true;
        CreatedOn = DateTime.UtcNow;
    }
}
