using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Session;

public class SessionDetailDto
{
    public Guid Id { get; set; }
    public DateTime SessionDate { get; set; }
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public SessionStatus Status { get; set; }
    public int RegisteredPlayersCount { get; set; }
    public IEnumerable<UserDto> RegisteredPlayers { get; set; }
    public bool IsFullyBooked => RegisteredPlayersCount >= MaxCapacity;
    public decimal AvailableSpots => MaxCapacity - RegisteredPlayersCount;
}
