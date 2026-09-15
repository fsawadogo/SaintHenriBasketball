using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// A player whose volunteer role an admin is changing.
public record StaffRoleTarget(Guid Id, string FirstName, string LastName, bool IsAdmin, bool IsDeactivated, StaffRole StaffRole);

/// A session a court captain can pick. SessionDate, StartTime and EndTime are Montreal local.
public record CourtSessionRow(
    Guid Id,
    DateTime SessionDate,
    string StartTime,
    string EndTime,
    string? Location,
    SessionStatus Status,
    int RegisteredPlayersCount,
    int MaxCapacity);

public record CourtPlayerRow(Guid Id, string FirstName, string LastName, string? Email);

public interface IVolunteerRolesRepository
{
    Task<StaffRoleTarget?> GetStaffRoleTargetAsync(Guid userId);

    /// Changes only the StaffRole column.
    Task SetStaffRoleAsync(Guid userId, StaffRole role);

    /// Sessions dated from <paramref name="sessionDateFrom"/> to <paramref name="sessionDateTo"/> (inclusive calendar dates),
    /// excluding cancelled sessions, by date then start time.
    Task<IReadOnlyList<CourtSessionRow>> GetCourtSessionsAsync(DateTime sessionDateFrom, DateTime sessionDateTo);

    /// Players who aren't deactivated whose name, email or username contains <paramref name="term"/>, by last then first name.
    Task<IReadOnlyList<CourtPlayerRow>> SearchActivePlayersAsync(string term, int max);
}
