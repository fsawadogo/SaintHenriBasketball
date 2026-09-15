namespace SaintHenriBasketball.Domain.Enums;

/// Limited admin access for club volunteers. Only takes effect while the `volunteer-roles` flag is on;
/// admins already have full access and never need one.
public enum StaffRole
{
    None = 0,
    /// Takes attendance at the court.
    CourtCaptain = 1,
    /// Sees payments and money reports.
    Treasurer = 2,
}
