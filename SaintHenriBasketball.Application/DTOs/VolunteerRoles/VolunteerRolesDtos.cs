using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.VolunteerRoles;

public class SetStaffRoleRequest
{
    /// "None", "CourtCaptain" or "Treasurer" (case-insensitive).
    public string? Role { get; set; }
}

/// The result of setting a player's volunteer role.
public record StaffRoleChange(Guid UserId, string PlayerName, StaffRole Before, StaffRole After)
{
    public bool Changed => Before != After;
}

/// A recent or upcoming session a court captain can open the roster for.
public class CourtSessionDto
{
    public Guid Id { get; set; }
    /// Montreal calendar date, "yyyy-MM-dd".
    public string SessionDate { get; set; } = string.Empty;
    /// Montreal local times as stored on the session ("HH:mm").
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    /// The start as a UTC instant.
    public DateTime StartsAt { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Registered { get; set; }
    public int Capacity { get; set; }
}

/// The minimum a court captain needs to add a walk-in.
public class CourtPlayerDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
}
