using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Waitlist;

public class WaitlistDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public int Position { get; set; }
    public WaitlistStatus Status { get; set; }
    public DateTime RegistrationDate { get; set; }
    public string? Notes { get; set; }
}

public class JoinWaitlistDto
{
    public Guid SessionId { get; set; }
    public string? Notes { get; set; }
}
