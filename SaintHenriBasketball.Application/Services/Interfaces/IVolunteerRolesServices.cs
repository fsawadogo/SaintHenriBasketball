using SaintHenriBasketball.Application.DTOs.VolunteerRoles;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// Gives or removes a player's volunteer role. Callers audit the change and clear the player's cached auth state.
public interface IStaffRoleService
{
    /// Throws ValidationException for an unknown role, an admin or a deactivated player (clearing to None is always allowed),
    /// and NotFoundException for an unknown player.
    Task<StaffRoleChange> SetStaffRoleAsync(Guid userId, string? role);
}

/// What a court captain needs outside the roster itself: a session to pick and a player search for walk-ins.
public interface ICourtCaptainService
{
    /// Sessions from 7 days ago to 14 days ahead (Montreal dates), not cancelled.
    Task<IReadOnlyList<CourtSessionDto>> GetSessionsAsync();

    /// Active players matching the search (at least 2 characters), at most 20.
    Task<IReadOnlyList<CourtPlayerDto>> SearchPlayersAsync(string? search);
}
