namespace SaintHenriBasketball.Domain.Entities;

public class SessionRecap
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedOn { get; private set; }

    private SessionRecap() { } // EF Core

    public SessionRecap(Guid sessionId, string photoUrl, string? caption, Guid createdBy)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        PhotoUrl = photoUrl;
        Caption = caption;
        CreatedBy = createdBy;
        CreatedOn = DateTime.UtcNow;
    }
}
