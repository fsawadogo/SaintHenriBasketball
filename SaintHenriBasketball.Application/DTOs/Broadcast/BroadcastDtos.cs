namespace SaintHenriBasketball.Application.DTOs.Broadcast;

public enum BroadcastAudience
{
    All = 0,
    SeasonHolders = 1,
    DropInOnly = 2,
    RecentNoShows = 3,
}

public class BroadcastAudiencePreviewDto
{
    public int RecipientCount { get; set; }
    public List<string> SampleEmails { get; set; } = new();
}

public class SendBroadcastRequestDto
{
    public BroadcastAudience Audience { get; set; }
    public string Subject { get; set; } = string.Empty;
    /// Optional French subject; French-speaking recipients fall back to Subject.
    public string? SubjectFr { get; set; }
    public string BodyEn { get; set; } = string.Empty;
    public string? BodyFr { get; set; }
}

public class SendBroadcastResultDto
{
    /// Email recipients. When Queued, delivery counts are recorded in the broadcast history.
    public int Attempted { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public bool Queued { get; set; }
    /// The history entry for this broadcast.
    public Guid? BroadcastId { get; set; }
}

/// BroadcastId links delivery back to the history entry; older queued items may not have one.
public record QueuedBroadcast(SendBroadcastRequestDto Request, Guid? AdminId, string AdminName, Guid? BroadcastId = null);

public class BroadcastHistoryItemDto
{
    public Guid Id { get; set; }
    public BroadcastAudience Audience { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string SentByName { get; set; } = string.Empty;
    public DateTime QueuedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// Queued, Sent or Failed.
    public string Status { get; set; } = string.Empty;
    public int Attempted { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
}

public class BroadcastDetailDto : BroadcastHistoryItemDto
{
    public string? SubjectFr { get; set; }
    public string BodyEn { get; set; } = string.Empty;
    public string? BodyFr { get; set; }
}

public class BroadcastHistoryPageDto
{
    public IReadOnlyList<BroadcastHistoryItemDto> Items { get; set; } = Array.Empty<BroadcastHistoryItemDto>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
