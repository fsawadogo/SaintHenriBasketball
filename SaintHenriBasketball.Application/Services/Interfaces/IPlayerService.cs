using SaintHenriBasketball.Application.DTOs;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IPlayerService
{
    Task<PlayerDto> CreatePlayerAsync(CreatePlayerDto createPlayerDto);
    Task<PlayerDto> GetPlayerAsync(Guid id);
    Task<IReadOnlyList<PlayerDto>> GetAllPlayersAsync();
    Task UpdatePlayerAsync(Guid id, UpdatePlayerDto updatePlayerDto);
    Task DeletePlayerAsync(Guid id);
}
