using System.ComponentModel.DataAnnotations;

namespace SaintHenriBasketball.Application.DTOs.WaitlistAdmin;

public class WaitlistSessionHeaderDto
{
    public Guid SessionId { get; set; }
    /// Montreal calendar date, yyyy-MM-dd.
    public string SessionDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string? Location { get; set; }
    /// Open, Full, Cancelled or Completed.
    public string Status { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public int Capacity { get; set; }
    /// Players occupying a place (reservation or attending RSVP).
    public int RegisteredCount { get; set; }
    /// Offers not yet claimed or expired; each holds a place.
    public int OffersOutstanding { get; set; }
    /// Capacity minus registered minus outstanding offers, never below zero.
    public int OpenSpots { get; set; }
}

public class WaitlistAdminEntryDto
{
    public Guid EntryId { get; set; }
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    /// 1-based place in line; the value the reorder endpoint takes.
    public int Position { get; set; }
    /// Waiting or Offered.
    public string Status { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public DateTime? OfferSentAt { get; set; }
    public DateTime? OfferExpiresAt { get; set; }
}

public class WaitlistSessionDto
{
    public WaitlistSessionHeaderDto Session { get; set; } = new();
    public IReadOnlyList<WaitlistAdminEntryDto> Entries { get; set; } = Array.Empty<WaitlistAdminEntryDto>();
}

public class WaitlistDemandSessionDto
{
    public Guid SessionId { get; set; }
    public string SessionDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public int Capacity { get; set; }
    public int Registered { get; set; }
    /// Entries still waiting (not counting outstanding offers).
    public int Waitlisted { get; set; }
    public int OffersOutstanding { get; set; }
    public int OpenSpots { get; set; }
    /// Registered / capacity as a fraction (1 = full), rounded to 4 decimals; 0 when capacity is 0.
    public double FillRate { get; set; }
}

public class WaitlistDemandDto
{
    /// First and last Montreal dates covered, yyyy-MM-dd.
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public int Weeks { get; set; }
    public IReadOnlyList<WaitlistDemandSessionDto> Sessions { get; set; } = Array.Empty<WaitlistDemandSessionDto>();
}

public class WaitlistOfferResultDto
{
    public WaitlistAdminEntryDto Entry { get; set; } = new();
    public WaitlistSessionHeaderDto Session { get; set; } = new();
    /// False when bookings plus other outstanding offers already filled the session when the offer was sent.
    public bool HadOpenSpot { get; set; }
    /// Offers held by other players on this session.
    public int OtherOffersOutstanding { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class MoveWaitlistEntryDto
{
    /// Target 1-based position; clamped to the line.
    [Required]
    public int? Position { get; set; }
}
