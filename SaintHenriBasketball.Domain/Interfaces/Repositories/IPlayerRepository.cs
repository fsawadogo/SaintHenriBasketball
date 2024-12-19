using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IPlayerRepository : IGenericRepository<Player>
{
    Task<Player> GetPlayerWithRegistrationsAsync(Guid id);
    Task<Player> GetPlayerByEmailAsync(string email);
    Task<IReadOnlyList<Player>> GetPlayersBySessionAsync(Guid sessionId);
}
