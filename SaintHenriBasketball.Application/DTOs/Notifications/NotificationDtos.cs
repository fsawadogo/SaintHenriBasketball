namespace SaintHenriBasketball.Application.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }
    public int Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class UnreadCountDto
{
    public int Count { get; set; }
}

public class NotificationPreferencesDto
{
    public bool EmailEnabled { get; set; }
    public bool SmsOptIn { get; set; }
    public string? PhoneNumber { get; set; }
    public bool InAppEnabled { get; set; }
}
