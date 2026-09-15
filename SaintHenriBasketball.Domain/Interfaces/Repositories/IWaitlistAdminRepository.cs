using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// A waiting or offered entry (User loaded). OfferSentAt is derived, not stored: see WaitlistAdminRepository.
public sealed record WaitlistLineEntry(Waitlist Entry, DateTime? OfferSentAt);

/// A session, the players occupying a place, and its current line in order.
public sealed record WaitlistSessionSnapshot(Session Session, int Occupied, IReadOnlyList<WaitlistLineEntry> Entries);

public sealed record WaitlistDemandRow(Session Session, int Occupied, int Waiting, int OffersOutstanding);

/// Read side of admin waitlist management. Changes go through IParticipationRepository (session-row lock).
public interface IWaitlistAdminRepository
{
    Task<WaitlistSessionSnapshot?> GetSessionWaitlistAsync(Guid sessionId, DateTime nowUtc);
    Task<IReadOnlyList<WaitlistDemandRow>> GetDemandAsync(DateTime fromDate, DateTime toDateExclusive, DateTime nowUtc);
}
