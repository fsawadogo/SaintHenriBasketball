namespace SaintHenriBasketball.Application.DTOs.PlayerTimeline;

/// The event types a timeline can be filtered by, in display order.
public static class PlayerTimelineEventTypes
{
    public const string Payment = "payment";
    public const string Credit = "credit";
    public const string Referral = "referral";
    public const string Waiver = "waiver";
    public const string Attendance = "attendance";
    public const string Message = "message";
    public const string Notification = "notification";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<string> All = new[] { Payment, Credit, Referral, Waiver, Attendance, Message, Notification, Admin };
}

public class PlayerTimelineQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    /// Event types to include; each entry may also be a comma-separated list. Empty means all.
    public IReadOnlyList<string>? Types { get; set; }
}

public class PlayerTimelineDto
{
    public Guid UserId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public bool IsDeactivated { get; set; }
    public bool IsAnonymized { get; set; }

    public int Page { get; set; }
    public int PageSize { get; set; }
    /// Events matching the type filter.
    public int Total { get; set; }
    public bool HasMore { get; set; }
    /// The types included in this response (all of them when no filter was given).
    public IReadOnlyList<string> Types { get; set; } = Array.Empty<string>();

    /// Event count for every type, ignoring the filter, so the UI can label its filter choices.
    public IReadOnlyList<PlayerTimelineTypeCountDto> Summary { get; set; } = Array.Empty<PlayerTimelineTypeCountDto>();
    public IReadOnlyList<PlayerTimelineEventDto> Events { get; set; } = Array.Empty<PlayerTimelineEventDto>();

    public PlayerTimelineNotesDto Notes { get; set; } = new();
    /// True when emails can't be matched to the player (their email was erased by anonymization).
    public bool MessagesUnavailable { get; set; }
}

public class PlayerTimelineTypeCountDto
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class PlayerTimelineEventDto
{
    /// Stable across requests, e.g. "payment:{id}:completed".
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    /// What happened within the type, e.g. "created", "completed", "refunded", "checkIn".
    public string Subtype { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    /// The payment, session or season the event is about, when useful.
    public Guid? RelatedId { get; set; }
    public string? ActorName { get; set; }
}

public class PlayerTimelineNotesDto
{
    public string? Text { get; set; }
    /// From the most recent notes-related audit entry, when there is one.
    public DateTime? LastChangedAt { get; set; }
    public string? LastChangedBy { get; set; }
}
