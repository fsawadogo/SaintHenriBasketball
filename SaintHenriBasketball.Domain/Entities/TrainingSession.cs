using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Exceptions;

namespace SaintHenriBasketball.Domain.Entities;

public class TrainingSession : BaseEntity
{
    public DateTime SessionDate { get; private set; }
    public int MaxCapacity { get; private set; }
    public decimal DropInPrice { get; private set; }
    public SessionStatus Status { get; private set; }
    private readonly List<SessionRegistration> _registrations = new();

    public IReadOnlyCollection<SessionRegistration> Registrations => _registrations.AsReadOnly();

    private TrainingSession() { } // For EF Core

    public TrainingSession(DateTime sessionDate, int maxCapacity, decimal dropInPrice)
    {
        Id = Guid.NewGuid();
        SessionDate = sessionDate;
        MaxCapacity = maxCapacity;
        DropInPrice = dropInPrice;
        Status = SessionStatus.Scheduled;
        CreatedOn = DateTime.UtcNow;
    }

    public void UpdateStatus(SessionStatus newStatus)
    {
        Status = newStatus;
        ModifiedOn = DateTime.UtcNow;
    }

    public bool CanRegister()
    {
        return Status == SessionStatus.Scheduled &&
               _registrations.Count < MaxCapacity &&
               SessionDate > DateTime.UtcNow;
    }

    public void AddRegistration(SessionRegistration registration)
    {
        if (!CanRegister())
            throw new DomainException("Cannot register for this session");

        _registrations.Add(registration);
        ModifiedOn = DateTime.UtcNow;
    }
}
