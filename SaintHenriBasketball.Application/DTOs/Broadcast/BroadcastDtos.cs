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
    public string BodyEn { get; set; } = string.Empty;
    public string? BodyFr { get; set; }
}

public class SendBroadcastResultDto
{
    public int Attempted { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
}
