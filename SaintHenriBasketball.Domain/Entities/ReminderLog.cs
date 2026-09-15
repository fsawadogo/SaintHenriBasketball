namespace SaintHenriBasketball.Domain.Entities;

/// One payment reminder an admin tried to send for one outstanding debt, whether it went out, failed or was skipped.
public class ReminderLog
{
    public const int MaxReasonLength = 300;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    /// The pending payment the reminder was about (drop-ins, transfers, and season fees that already have a payment).
    public Guid? PaymentId { get; set; }
    /// The season a season-fee reminder was about.
    public Guid? SeasonId { get; set; }
    /// <see cref="ReminderKinds"/>.
    public string Kind { get; set; } = string.Empty;
    public string Channel { get; set; } = ReminderChannels.Email;
    /// <see cref="ReminderStatuses"/>.
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public Guid? SentByUserId { get; set; }
}

public static class ReminderKinds
{
    public const string SeasonFee = "SeasonFee";
    public const string DropIn = "DropIn";
    public const string Transfer = "Transfer";

    public static readonly IReadOnlyList<string> All = new[] { SeasonFee, DropIn, Transfer };
}

public static class ReminderStatuses
{
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

public static class ReminderChannels
{
    public const string Email = "email";
}
