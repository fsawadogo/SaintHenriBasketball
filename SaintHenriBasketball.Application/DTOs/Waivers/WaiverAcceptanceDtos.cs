namespace SaintHenriBasketball.Application.DTOs.Waivers;

public class WaiverAcceptanceDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime AcceptedAt { get; set; }
}

public class WaiverAcceptancesDto
{
    public int Version { get; set; }
    public int AcceptedCount { get; set; }
    /// Confirmed accounts that haven't accepted this version.
    public int PendingCount { get; set; }
    public List<WaiverAcceptanceDto> Acceptances { get; set; } = new();
}
