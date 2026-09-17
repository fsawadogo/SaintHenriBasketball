using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// Counts for one session of a season: places reserved and players marked attending.
public record SeasonDashboardSessionRow(Session Session, int Reserved, int Attended, decimal DropInsCollected, int DropInsPending);

public interface ISeasonDashboardRepository
{
    /// Seasons newest first, for the season picker.
    Task<IReadOnlyList<Season>> GetSeasonsAsync();

    Task<Season?> GetSeasonAsync(Guid seasonId);

    /// The season covering <paramref name="todayLocal"/>, else the next one to start, else the most recent.
    Task<Season?> GetCurrentOrNextSeasonAsync(DateTime todayLocal);

    /// Payments belonging to the season: season payments linked to it, and drop-ins for its sessions.
    Task<IReadOnlyList<Payment>> GetSeasonPaymentsAsync(Guid seasonId, DateTime startDate, DateTime endDateExclusive);

    /// How many players hold a paid pass, and how many chose the season plan without paying.
    Task<(int Paid, int UnpaidChoices)> GetPassCountsAsync(Guid seasonId);

    /// One row per session inside the season's dates, in date order. Four queries, no per-session loop.
    Task<IReadOnlyList<SeasonDashboardSessionRow>> GetSessionRowsAsync(Guid seasonId, DateTime startDate, DateTime endDateExclusive);
}
