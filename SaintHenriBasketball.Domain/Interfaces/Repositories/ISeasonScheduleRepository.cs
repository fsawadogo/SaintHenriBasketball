using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// Reads and writes for the season schedule wizard.
public interface ISeasonScheduleRepository
{
    /// Sessions between the two calendar dates, both included, that aren't cancelled.
    Task<IReadOnlyList<Session>> GetActiveSessionsBetweenAsync(DateTime fromDate, DateTime toDate);

    /// Saves the season and its sessions in one call, so either all of them are saved or none are.
    Task AddSeasonWithSessionsAsync(Season season, IReadOnlyList<Session> sessions);
}
