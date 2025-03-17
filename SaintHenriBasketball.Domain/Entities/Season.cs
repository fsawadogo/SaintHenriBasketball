using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class Season
{
    public Guid Id { get; private set; }
    public string Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public SeasonStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedOn { get; private set; }
    public ICollection<SeasonRegistration> Registrations { get; private set; }

    private Season() { } // For EF Core

    public Season(DateTime startDate, DateTime endDate, decimal price, string? notes = null)
    {
        Id = Guid.NewGuid();
        StartDate = startDate;
        EndDate = endDate;
        Price = price;
        Status = SeasonStatus.Open;
        Notes = notes;
        CreatedOn = DateTime.UtcNow;
        Registrations = new List<SeasonRegistration>();
    }
}