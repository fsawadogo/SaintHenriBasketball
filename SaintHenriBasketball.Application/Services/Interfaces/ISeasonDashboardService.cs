using SaintHenriBasketball.Application.DTOs.SeasonDashboard;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISeasonDashboardService
{
    /// The dashboard for one season, or for the season running today when <paramref name="seasonId"/> is null.
    /// Returns null only when the club has no seasons at all.
    Task<SeasonDashboardDto?> GetAsync(Guid? seasonId);
}
