using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SessionCancellationService : ISessionCancellationService
{
    public const int MaxReasonLength = 300;
    public const string AlreadyCancelledMessage = "This session is already cancelled.";

    private readonly ISessionRepository _sessions;
    private readonly ISessionRegistrationRepository _registrations;
    private readonly IPaymentRepository _payments;
    private readonly ISessionService _sessionService;
    private readonly IPaymentService _paymentService;
    private readonly IPaymentRefundService _refunds;
    private readonly IStripeService _stripe;
    private readonly IEmailService _email;
    private readonly INotificationService _notifications;
    private readonly IWaitlistRepository _waitlist;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SessionCancellationService> _logger;

    public SessionCancellationService(
        ISessionRepository sessions,
        ISessionRegistrationRepository registrations,
        IPaymentRepository payments,
        ISessionService sessionService,
        IPaymentService paymentService,
        IPaymentRefundService refunds,
        IStripeService stripe,
        IEmailService email,
        INotificationService notifications,
        IWaitlistRepository waitlist,
        IConfiguration configuration,
        ILogger<SessionCancellationService> logger)
    {
        _sessions = sessions;
        _registrations = registrations;
        _payments = payments;
        _sessionService = sessionService;
        _paymentService = paymentService;
        _refunds = refunds;
        _stripe = stripe;
        _email = email;
        _notifications = notifications;
        _waitlist = waitlist;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SessionCancellationPreviewDto> PreviewAsync(Guid sessionId)
    {
        var session = await _sessions.GetByIdAsync(sessionId) ?? throw new NotFoundException(nameof(Session), sessionId);
        var registrations = await _registrations.GetBySessionIdAsync(sessionId);
        var payments = await _payments.GetBySessionAsync(sessionId);
        var paid = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
        return new SessionCancellationPreviewDto
        {
            SessionId = sessionId,
            SessionDate = session.SessionDate,
            RegisteredPlayers = registrations.Count,
            PendingPayments = payments.Count(p => p.Status == PaymentStatus.Pending),
            PaidPayments = paid.Count,
            PaidAmount = paid.Sum(p => p.Amount),
        };
    }

    public async Task<SessionCancellationResultDto> CancelAsync(Guid sessionId, CancelSessionRequest request)
    {
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason?.Length > MaxReasonLength)
            throw new ValidationException($"Keep the reason under {MaxReasonLength} characters.");

        var session = await _sessions.GetByIdAsync(sessionId) ?? throw new NotFoundException(nameof(Session), sessionId);
        if (session.Status == SessionStatus.Cancelled)
            throw new ValidationException(AlreadyCancelledMessage);

        // Read who is affected before the status changes; CancelSessionAsync refuses completed sessions.
        var registrations = await _registrations.GetBySessionIdAsync(sessionId);
        var payments = await _payments.GetBySessionAsync(sessionId);
        await _sessionService.CancelSessionAsync(sessionId);

        var result = new SessionCancellationResultDto { SessionId = sessionId };
        // What each player is owed, or no longer owes, keyed by player.
        var outcomes = new Dictionary<Guid, (CancellationMoney Money, decimal Amount)>();

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending))
        {
            // A checkout that has already completed means the player's money is on its way, even
            // though the payment still reads Pending. Writing it off here would leave the club
            // holding money its own records call Failed — and a failed payment cannot be refunded.
            // Leave it Pending: the Stripe webhook completes it, and the player is told the money
            // is still with the club.
            if (!await CloseOpenCardCheckoutAsync(payment))
            {
                result.PaidNotRefunded++;
                outcomes[payment.UserId] = (CancellationMoney.StillHeld, payment.Amount);
                _logger.LogWarning(
                    "Payment {PaymentId} left pending on cancellation of {SessionId}: its checkout has already completed",
                    payment.Id, sessionId);
                continue;
            }

            if (await _paymentService.VoidForCancelledSessionAsync(payment.Id))
            {
                result.PaymentsVoided++;
                outcomes[payment.UserId] = (CancellationMoney.Cancelled, payment.Amount);
            }
        }

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Completed))
        {
            if (!request.RefundPaidToCredit)
            {
                result.PaidNotRefunded++;
                // The club keeps the money for now, so the email says exactly that.
                outcomes[payment.UserId] = (CancellationMoney.StillHeld, payment.Amount);
                continue;
            }
            try
            {
                await _refunds.RefundAsync(payment.Id, new RefundPaymentDto
                {
                    Method = RefundMethod.AccountCredit,
                    Reason = reason == null ? "Session cancelled" : $"Session cancelled: {reason}",
                });
                result.PaymentsRefunded++;
                result.RefundedAmount += payment.Amount;
                outcomes[payment.UserId] = (CancellationMoney.Credited, payment.Amount);
            }
            catch (Exception ex) when (ex is ValidationException or NotFoundException)
            {
                _logger.LogWarning(ex, "Could not refund payment {PaymentId} for cancelled session {SessionId}", payment.Id, sessionId);
                result.PaidNotRefunded++;
                outcomes[payment.UserId] = (CancellationMoney.StillHeld, payment.Amount);
            }
        }

        var players = registrations
            .Select(r => r.User)
            .Where(u => u is { IsDeactivated: false })
            .DistinctBy(u => u.Id)
            .ToList();
        result.PlayersNotified = players.Count;

        // Anyone waiting for a place was waiting for nothing: tell them, and close their entry.
        var waiting = await ClearWaitlistAsync(sessionId);
        result.WaitingPlayersNotified = waiting.Count;

        var next = await FindNextSessionAsync(session);
        var appUrl = _configuration["AppUrl"] ?? "https://sainthenribasketball.com";

        SessionCancellationEmailModel Model(ApplicationUser player, bool waitingForPlace) => new()
        {
            FirstName = player.FirstName,
            SessionDate = session.SessionDate,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Location = session.Location,
            Reason = reason,
            WasWaiting = waitingForPlace,
            Money = waitingForPlace
                ? CancellationMoney.Nothing
                : outcomes.TryGetValue(player.Id, out var outcome)
                    ? outcome.Money
                    : player.PaymentPlan == PaymentPlan.Season ? CancellationMoney.CoveredByPass : CancellationMoney.Nothing,
            Amount = outcomes.TryGetValue(player.Id, out var paid) ? paid.Amount : 0m,
            NextSessionId = next?.Id,
            NextSessionDate = next?.SessionDate,
            NextStartTime = next?.StartTime,
            NextEndTime = next?.EndTime,
            NextSpotsLeft = next == null ? null : Math.Max(0, next.MaxCapacity - next.RegisteredPlayersCount),
            AppUrl = appUrl,
        };

        var recipients = players.Select(p => (User: p, Model: Model(p, false)))
            .Concat(waiting.Select(p => (User: p, Model: Model(p, true))))
            .ToList();

        try
        {
            // Sent whatever the player's email preference says: a cancelled session is not marketing.
            await _email.SendSessionCancellationEmailsAsync(recipients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancellation emails failed for session {SessionId}", sessionId);
        }

        foreach (var (player, waitingForPlace) in players.Select(p => (p, false)).Concat(waiting.Select(p => (p, true))))
        {
            try
            {
                await _notifications.CreateAsync(player.Id, NotificationType.SessionCancelled,
                    title: "Session cancelled",
                    body: (waitingForPlace
                            ? $"The {session.SessionDate:MMMM d} session you were waiting for is cancelled."
                            : $"The {session.SessionDate:MMMM d} session is cancelled.")
                        + (reason == null ? "" : $" {reason}"),
                    url: "/schedule");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not notify player {UserId} about cancelled session {SessionId}", player.Id, sessionId);
            }
        }

        return result;
    }

    /// Closes every open waitlist entry for a cancelled session and returns the players who were waiting.
    /// Left open, they would sit in a queue for a session that will never run.
    private async Task<List<ApplicationUser>> ClearWaitlistAsync(Guid sessionId)
    {
        var waiting = new List<ApplicationUser>();
        try
        {
            var entries = await _waitlist.GetBySessionAsync(sessionId);
            foreach (var entry in entries.Where(e => e.Status is WaitlistStatus.Waiting or WaitlistStatus.Offered))
            {
                entry.Status = WaitlistStatus.Cancelled;
                entry.OfferExpiresAt = null;
                await _waitlist.UpdateAsync(entry);
                if (entry.User is { IsDeactivated: false }) waiting.Add(entry.User);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not clear the waitlist for cancelled session {SessionId}", sessionId);
        }
        return waiting.DistinctBy(u => u.Id).ToList();
    }

    /// The next session still going ahead after this one, so the email has somewhere to send people.
    private async Task<Session?> FindNextSessionAsync(Session cancelled)
    {
        try
        {
            var upcoming = await _sessions.GetUpcomingSessionsAsync();
            return upcoming
                .Where(s => s.Id != cancelled.Id && s.Status != SessionStatus.Cancelled && s.SessionDate >= cancelled.SessionDate)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not find a session to suggest after {SessionId}", cancelled.Id);
            return null;
        }
    }

    /// An unpaid card checkout would otherwise still accept money for a session that no longer happens.
    /// <summary>
    /// Shuts an open card checkout. Returns whether the payment is safe to write off: false when
    /// the checkout has already completed, or when we could not find out.
    /// </summary>
    private async Task<bool> CloseOpenCardCheckoutAsync(Payment payment)
    {
        if (payment.Reference?.StartsWith("cs_", StringComparison.Ordinal) != true) return true;
        try
        {
            return await _stripe.ExpireCheckoutAsync(payment.Reference);
        }
        catch (Exception ex)
        {
            // Not knowing is not the same as knowing nothing was paid. Keep the payment.
            _logger.LogWarning(ex, "Could not expire Stripe checkout for payment {PaymentId}; leaving it pending", payment.Id);
            return false;
        }
    }
}
