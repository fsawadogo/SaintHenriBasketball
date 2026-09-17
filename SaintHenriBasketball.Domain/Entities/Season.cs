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
    /// How many players may hold a season pass for this season. A spot is held by a completed
    /// payment, never by an unpaid choice. Defaults to 15.
    public int SeasonPassCapacity { get; set; } = DefaultSeasonPassCapacity;
    public DateTime CreatedOn { get; private set; }

    public const int DefaultSeasonPassCapacity = 15;
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