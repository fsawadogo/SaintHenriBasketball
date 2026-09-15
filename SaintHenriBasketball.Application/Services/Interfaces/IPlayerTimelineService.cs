using SaintHenriBasketball.Application.DTOs.PlayerTimeline;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IPlayerTimelineService
{
    /// <summary>
    /// One page of everything that happened with a player, newest first, with per-type counts and the admin notes.
    /// Throws NotFoundException for an unknown player and ValidationException for an unknown type or a page too deep.
    /// </summary>
    Task<PlayerTimelineDto> GetTimelineAsync(Guid userId, PlayerTimelineQuery query);
}
