namespace SaintHenriBasketball.Application.Services.Interfaces;

/// What one backfill run did, or would do.
public class BookingConfirmationBackfillResultDto
{
    /// True when nothing was sent, only counted.
    public bool DryRun { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    /// Why the run did nothing at all, when it did nothing.
    public string? Outcome { get; set; }
    /// Who would receive it, on a dry run: "name — session date".
    public List<string> Recipients { get; set; } = new();
}

/// <summary>
/// Sends the booking confirmation to players whose place was reserved before the confirmation email
/// existed, or whose email failed at the time.
///
/// Only registrations for sessions still to come, and only those never confirmed — a registration is
/// stamped once its email goes out, so running this twice cannot reach the same player again.
/// </summary>
public interface IBookingConfirmationBackfillService
{
    Task<BookingConfirmationBackfillResultDto> RunAsync(Guid? sessionId, bool dryRun = false);
}
