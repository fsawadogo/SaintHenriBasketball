using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// Read queries for the `season-rollover` feature. Seasons are still added through ISeasonRepository
/// and sessions through the session generator; this only answers the questions a rollover asks.
public interface ISeasonRolloverRepository
{
    /// Seasons whose date range shares at least one calendar day with [startDate, endDate].
    Task<IReadOnlyList<Season>> GetOverlappingSeasonsAsync(DateTime startDate, DateTime endDate);

    /// True when a season starts and ends on these calendar dates.
    Task<bool> SeasonExistsWithDatesAsync(DateTime startDate, DateTime endDate);

    /// Sessions on any calendar date from fromDate to toDate inclusive, oldest first.
    Task<IReadOnlyList<Session>> GetSessionsBetweenAsync(DateTime fromDate, DateTime toDate);

    /// Players with at least one completed payment linked to the season.
    Task<IReadOnlyList<ApplicationUser>> GetPlayersWithCompletedSeasonPaymentAsync(Guid seasonId);

    /// Players whose payment plan is Season.
    Task<IReadOnlyList<ApplicationUser>> GetSeasonPlanPlayersAsync();

    /// Players who already have a pending or completed payment for the season.
    Task<IReadOnlyList<Guid>> GetUserIdsWithSeasonPaymentAsync(Guid seasonId);
}
