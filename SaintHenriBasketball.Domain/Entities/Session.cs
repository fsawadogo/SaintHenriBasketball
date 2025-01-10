using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Exceptions;

namespace SaintHenriBasketball.Domain.Entities;

public class Session
{
    public Guid Id { get; private set; }
    public DateTime SessionDate { get; set; }
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public SessionStatus Status { get; set; }
    public int RegisteredPlayersCount { get; set; }
    public ICollection<SessionRegistration> Registrations { get; set; }
    public DateTime CreatedOn { get; private set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public string Location { get; set; }

    private Session() { } // For EF Core

    public Session(DateTime sessionDate, int maxCapacity, decimal dropInPrice, string startTime, string endTime, string location)
    {
        Id = Guid.NewGuid();
        SessionDate = sessionDate;
        MaxCapacity = maxCapacity;
        DropInPrice = dropInPrice;
        Status = SessionStatus.Open;
        RegisteredPlayersCount = 0;
        Registrations = new List<SessionRegistration>();
        CreatedOn = DateTime.UtcNow;
        StartTime = startTime ?? throw new ArgumentNullException(nameof(startTime));
        EndTime = endTime ?? throw new ArgumentNullException(nameof(endTime));
        Location = location ?? throw new ArgumentNullException(nameof(location));
    }
}