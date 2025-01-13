using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// <summary>
/// Service for managing basketball seasons
/// </summary>
public interface ISeasonService
{
    /// <summary>
    /// Creates a new season
    /// </summary>
    Task<SeasonDto> CreateSeasonAsync(CreateSeasonDto createSeasonDto);

    /// <summary>
    /// Gets a specific season by ID
    /// </summary>
    Task<SeasonDto> GetSeasonAsync(Guid id);

    /// <summary>
    /// Gets all seasons
    /// </summary>
    Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync();

    /// <summary>
    /// Gets the current active season
    /// </summary>
    Task<SeasonDto> GetCurrentSeasonAsync();

    /// <summary>
    /// Updates a season
    /// </summary>
    Task UpdateSeasonAsync(Guid id, UpdateSeasonDto updateSeasonDto);

    /// <summary>
    /// Updates a season's status
    /// </summary>
    Task UpdateSeasonStatusAsync(Guid id, SeasonStatus status);

    /// <summary>
    /// Deletes a season
    /// </summary>
    Task DeleteSeasonAsync(Guid id);

    /// <summary>
    /// Registers a user for a season
    /// </summary>
    Task<SeasonDto> RegisterUserForSeasonAsync(Guid seasonId, Guid userId);

    /// <summary>
    /// Unregisters a user from a season
    /// </summary>
    Task UnregisterUserFromSeasonAsync(Guid seasonId, Guid userId);

    /// <summary>
    /// Gets all users registered for a season
    /// </summary>
    Task<IEnumerable<SeasonUserDto>> GetRegisteredUsersAsync(Guid seasonId);
}