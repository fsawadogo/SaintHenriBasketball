using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// When a reminder was last sent for one debt (UserId + Kind + PaymentId/SeasonId).
public record ReminderLastSent(Guid UserId, string Kind, Guid? PaymentId, Guid? SeasonId, DateTime SentAt);

/// A reminder history row with the player it was sent to.
public record ReminderLogEntry(ReminderLog Log, string? FirstName, string? LastName, string? Email);

public interface IOutstandingBalancesRepository
{
    /// Seasons with a fee that haven't ended on <paramref name="todayLocal"/> and have either started or are open.
    Task<IReadOnlyList<Season>> GetBillableSeasonsAsync(DateTime todayLocal);

    /// Registrations for the given seasons, with the player.
    Task<IReadOnlyList<SeasonRegistration>> GetSeasonRegistrationsAsync(IReadOnlyCollection<Guid> seasonIds);

    /// Players on the Season plan who are not deactivated.
    Task<IReadOnlyList<ApplicationUser>> GetActiveSeasonPlanPlayersAsync();

    /// Season-plan payments linked to the given seasons, any status.
    Task<IReadOnlyList<Payment>> GetSeasonPaymentsAsync(IReadOnlyCollection<Guid> seasonIds);

    /// Players with an older season payment that isn't linked to a season (and isn't failed or refunded).
    Task<IReadOnlyCollection<Guid>> GetUsersWithUnlinkedSeasonPaymentsAsync();

    /// Latest Sent reminder per debt for these players, optionally only those sent at or after <paramref name="since"/>.
    Task<IReadOnlyList<ReminderLastSent>> GetLastSentAsync(IReadOnlyCollection<Guid> userIds, DateTime? since = null);

    Task AddLogsAsync(IEnumerable<ReminderLog> logs);

    Task<bool> UserExistsAsync(Guid userId);

    /// One page of reminder history, newest first, and how many match in total.
    Task<(IReadOnlyList<ReminderLogEntry> Items, int Total)> SearchLogsAsync(Guid? userId, int page, int pageSize);
}
