using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class BookingConfirmationBackfillService : IBookingConfirmationBackfillService
{
    public const string FlagOffOutcome = "The booking-confirmation-email flag is off.";
    public const string NobodyOutcome = "Every upcoming booking has already been confirmed.";

    private readonly ISessionRegistrationRepository _registrations;
    private readonly IEmailService _email;
    private readonly IFeatureFlagService _flags;
    private readonly ILogger<BookingConfirmationBackfillService> _logger;

    public BookingConfirmationBackfillService(
        ISessionRegistrationRepository registrations,
        IEmailService email,
        IFeatureFlagService flags,
        ILogger<BookingConfirmationBackfillService> logger)
    {
        _registrations = registrations;
        _email = email;
        _flags = flags;
        _logger = logger;
    }

    public async Task<BookingConfirmationBackfillResultDto> RunAsync(Guid? sessionId, bool dryRun = false)
    {
        var result = new BookingConfirmationBackfillResultDto { DryRun = dryRun };

        if (!await _flags.IsEnabledAsync(FeatureFlagKeys.BookingConfirmationEmail))
        {
            result.Outcome = FlagOffOutcome;
            return result;
        }

        // Today, not now: a place booked for a session earlier today still deserves its confirmation.
        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        var pending = await _registrations.GetAwaitingConfirmationAsync(sessionId, today);

        if (pending.Count == 0)
        {
            result.Outcome = NobodyOutcome;
            return result;
        }

        foreach (var registration in pending)
        {
            var user = registration.User;
            var session = registration.Session;
            if (user is null || session is null || string.IsNullOrWhiteSpace(user.Email)) continue;

            if (dryRun)
            {
                result.Sent++;
                if (result.Recipients.Count < 200)
                    result.Recipients.Add($"{user.FirstName} {user.LastName} — {session.SessionDate:yyyy-MM-dd}");
                continue;
            }

            try
            {
                await _email.SendBookingConfirmationAsync(user, session);
                // Stamped only after a successful send, so a failure is retried by the next run.
                await _registrations.MarkConfirmationSentAsync(registration.Id, DateTime.UtcNow);
                result.Sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backfilled booking confirmation failed for {UserId} and session {SessionId}",
                    registration.UserId, registration.SessionId);
                result.Failed++;
            }
        }

        _logger.LogInformation("Booking confirmation backfill: {Sent} sent, {Failed} failed", result.Sent, result.Failed);
        return result;
    }
}
