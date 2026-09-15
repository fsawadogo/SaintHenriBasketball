using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// The player the timeline is about. Found whether active, deactivated or anonymized.
public record PlayerTimelineSubject(Guid Id, string FirstName, string LastName, string? Email, string? AdminNotes, bool IsDeactivated, DateTime? AnonymizedOn);

/// How many timeline events each source holds for one player.
public record PlayerTimelineSourceCounts(
    int Payments,
    int CompletedPayments,
    int RefundedPayments,
    int Credits,
    int ReferralsAsReferrer,
    int ReferralsAsReferee,
    int WaiverAcceptances,
    int AttendanceAnswers,
    int CheckIns,
    int Messages,
    int Notifications,
    int AdminChanges);

public static class PlayerTimelineTimes
{
    /// Timestamps before this are unset legacy values (e.g. a payment saved without CreatedAt).
    public static readonly DateTime MissingBefore = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}

/// <summary>
/// Read-only access to everything that happened with a player. Every <c>GetNewest…</c> method returns the
/// rows whose latest event is among the newest <c>limit</c> of that source, plus any rows tied with the last
/// one, so a caller merging several sources never misses an event inside its window.
/// </summary>
public interface IPlayerTimelineRepository
{
    Task<PlayerTimelineSubject?> GetPlayerAsync(Guid userId);

    /// Event counts per source. <paramref name="email"/> null means messages can't be matched (counted as 0).
    Task<PlayerTimelineSourceCounts> CountAsync(Guid userId, string? email);

    /// Payments with Session and Season loaded.
    Task<IReadOnlyList<Payment>> GetNewestPaymentsAsync(Guid userId, int limit);
    Task<IReadOnlyList<AccountCredit>> GetNewestCreditsAsync(Guid userId, int limit);
    /// Redemptions where the player is the referrer or the referee.
    Task<IReadOnlyList<ReferralRedemption>> GetNewestReferralsAsync(Guid userId, int limit);
    Task<IReadOnlyList<WaiverAcceptance>> GetNewestWaiverAcceptancesAsync(Guid userId, int limit);
    /// Attendance answers with Session loaded.
    Task<IReadOnlyList<SessionAttendance>> GetNewestAttendanceAsync(Guid userId, int limit);
    Task<IReadOnlyList<EmailLog>> GetNewestEmailLogsAsync(string email, int limit);
    Task<IReadOnlyList<Notification>> GetNewestNotificationsAsync(Guid userId, int limit);
    /// Audit entries recorded against the player (EntityId = player).
    Task<IReadOnlyList<AuditLog>> GetNewestAuditLogsAsync(Guid userId, int limit);

    /// The most recent audit entry against the player with one of <paramref name="actions"/>.
    Task<AuditLog?> GetLatestAuditAsync(Guid userId, IReadOnlyCollection<string> actions);

    /// "First Last" for each user id that exists.
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IEnumerable<Guid> userIds);
    Task<IReadOnlyDictionary<Guid, string>> GetReferralCodesAsync(IEnumerable<Guid> referralCodeIds);
    /// The credit granted for each redemption, when one was granted.
    Task<IReadOnlyDictionary<Guid, decimal>> GetReferralRewardsAsync(IEnumerable<Guid> redemptionIds);
}
