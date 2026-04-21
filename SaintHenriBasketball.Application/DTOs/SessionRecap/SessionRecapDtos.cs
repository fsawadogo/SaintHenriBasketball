namespace SaintHenriBasketball.Application.DTOs.SessionRecap;

public class SessionRecapDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? SessionDate { get; set; }
}

public class CreateSessionRecapDto
{
    public string PhotoUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
}
