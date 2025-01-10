using SaintHenriBasketball.Application.DTOs.Season;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISeasonService
{
    Task<SeasonDto> CreateSeasonAsync(CreateSeasonDto createSeasonDto);
    Task<SeasonDto> GetSeasonAsync(Guid id);
    Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync();
    Task UpdateSeasonAsync(Guid id, UpdateSeasonDto updateSeasonDto);
    Task DeleteSeasonAsync(Guid id);
    Task<SeasonDto> RegisterUserForSeasonAsync(Guid seasonId, Guid userId);
    Task UnregisterUserFromSeasonAsync(Guid seasonId, Guid userId);
}