using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

/// A club email sent from the broadcast composer, kept so admins can see what went out, who sent it and how delivery went.
public class BroadcastMessage
{
    public const int MaxSubjectLength = 200;

    public Guid Id { get; private set; } = Guid.NewGuid();
    /// The composer's audience choice (All, SeasonHolders, DropInOnly, RecentNoShows).
    public int Audience { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? SubjectFr { get; set; }
    public string BodyEn { get; set; } = string.Empty;
    public string? BodyFr { get; set; }
    public Guid? SentByUserId { get; set; }
    public string SentByName { get; set; } = string.Empty;
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public BroadcastStatus Status { get; set; } = BroadcastStatus.Queued;
    /// Email recipients when queued; delivery counts once sent.
    public int Attempted { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
}
