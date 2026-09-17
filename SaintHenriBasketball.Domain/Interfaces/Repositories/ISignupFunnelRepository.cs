namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// One player who signed up in the period, with the milestones they have reached.
/// Times are UTC; null means the player never got that far.
public record SignupFunnelRow(
    Guid UserId,
    DateTime RegisteredAt,
    bool EmailConfirmed,
    bool IsDeactivated,
    DateTime? FirstReservationAt,
    DateTime? FirstPaymentAt,
    DateTime? FirstAttendanceAt);

public interface ISignupFunnelRepository
{
    /// Players who signed up between the two instants, with their first reservation, payment and
    /// attendance. Admins are left out: they never go through signing up.
    Task<IReadOnlyList<SignupFunnelRow>> GetRowsAsync(DateTime fromUtc, DateTime toUtcExclusive);
}
