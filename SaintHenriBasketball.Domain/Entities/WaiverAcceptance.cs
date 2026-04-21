namespace SaintHenriBasketball.Domain.Entities;

public class WaiverAcceptance
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int WaiverVersion { get; private set; }
    public DateTime AcceptedAt { get; private set; }
    public string? IpAddress { get; private set; }

    private WaiverAcceptance() { }

    public WaiverAcceptance(Guid userId, int waiverVersion, string? ipAddress)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        WaiverVersion = waiverVersion;
        AcceptedAt = DateTime.UtcNow;
        IpAddress = ipAddress;
    }
}
