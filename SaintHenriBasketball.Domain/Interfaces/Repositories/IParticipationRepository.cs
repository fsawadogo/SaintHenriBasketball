using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IParticipationRepository
{
    Task<SessionRegistration> ReserveAsync(Guid sessionId, Guid userId);
    Task CancelAsync(Guid sessionId, Guid userId);
    Task<SessionAttendance> CheckInAsync(Guid sessionId, Guid userId);
    Task LeaveWaitlistAsync(Guid sessionId, Guid userId);
    Task<SessionAttendance> SetAttendanceAsync(Guid sessionId, Guid userId, bool attending, string? notes, string? reason);
    Task<Waitlist> JoinWaitlistAsync(Guid sessionId, Guid userId, string? notes);
    Task<Waitlist?> OfferNextAsync(Guid sessionId);
    Task<IReadOnlyList<Guid>> GetWaitlistSessionIdsAsync();
}
