using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class EmailLog
{
    public Guid Id { get; private set; }
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public EmailType EmailType { get; set; }
    public EmailLogStatus Status { get; set; }
    public DateTime SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RecipientName { get; set; }
    public Guid? SentByUserId { get; set; }

    private EmailLog() { } // EF Core

    public EmailLog(string recipient, string subject, EmailType emailType)
    {
        Id = Guid.NewGuid();
        Recipient = recipient;
        Subject = subject;
        EmailType = emailType;
        Status = EmailLogStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = EmailLogStatus.Failed;
        ErrorMessage = errorMessage;
    }
}

public enum EmailLogStatus
{
    Sent = 0,
    Failed = 1,
    Pending = 2
}
