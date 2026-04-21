using SaintHenriBasketball.Application.DTOs.Stats;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IPersonalStatsService
{
    Task<PersonalStatsDto> GetForUserAsync(Guid userId);
    Task<BadgesSummaryDto> GetBadgesSummaryAsync(Guid userId);
}
