using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs;

public class SessionRegistrationDto
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid SessionId { get; set; }
    public RegistrationStatus Status { get; set; }
    public string PlayerName { get; set; }
    public DateTime SessionDate { get; set; }
}