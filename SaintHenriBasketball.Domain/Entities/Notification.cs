namespace SaintHenriBasketball.Domain.Entities;

public enum NotificationType
{
    Generic = 0,
    PaymentCompleted = 1,
    SessionCancelled = 2,
    AdminBroadcast = 3,
    SessionReminder = 4,
}

public class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedOn { get; private set; }

    private Notification() { } // EF Core

    public Notification(Guid userId, NotificationType type, string title, string body, string? url = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Type = type;
        Title = title;
        Body = body;
        Url = url;
        CreatedOn = DateTime.UtcNow;
    }
}
