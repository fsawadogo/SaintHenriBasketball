using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionRepository
{
    Task<Session> GetByIdAsync(Guid id);
    Task AddAsync(Session session);
    Task UpdateAsync(Session session);
    Task<IReadOnlyList<Session>> GetUpcomingSessionsAsync();
    Task<IReadOnlyList<Session>> GetAvailableSessionsAsync();
    Task<IEnumerable<Session>> GetUserSessionsAsync(Guid userId);
    Task<IReadOnlyList<Session>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<int> GetRegistrationCountAsync(Guid sessionId);
    Task<bool> ExistsAsync(Guid id);
    Task<Session> GetClosestSessionAsync();
    Task<Session> GetNextSessionAsync();
    Task<IReadOnlyList<Session>> GetAllSessionsAsync();
    Task<int> CountSessionsBetweenAsync(DateTime from, DateTime to);

    /// Sessions dated between the two calendar dates, both included, with their registrations.
    Task<IReadOnlyList<Session>> GetSessionsBetweenDatesAsync(DateTime fromDate, DateTime toDate);

    /// Counts of the rows deleting the session would remove, or null when it doesn't exist.
    Task<SessionDeletionImpact?> GetDeletionImpactAsync(Guid sessionId);

    /// Deletes the session with its registrations, attendance, waitlist, feedback and recaps in one transaction.
    /// Returns false, deleting nothing, when the session is gone or has payments.
    Task<bool> DeleteWithDependentsAsync(Guid sessionId);

    /// <summary>
    /// Everyone registered for a session, and whether they have answered a reminder to confirm.
    ///
    /// Registering is what takes a place; confirming is only a reply to a reminder. A list built
    /// from confirmations alone leaves out most of the people who are actually coming.
    /// </summary>
    Task<IReadOnlyList<SessionRosterEntry>> GetRosterAsync(Guid sessionId);
}

/// One registered player, with whether they have confirmed they are coming.
public record SessionRosterEntry(Guid UserId, string FirstName, string LastName, bool Confirmed);


