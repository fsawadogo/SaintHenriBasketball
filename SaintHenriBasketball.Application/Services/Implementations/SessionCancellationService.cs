using Microsoft.Extensions.Logging;
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
        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending))
        {
            await CloseOpenCardCheckoutAsync(payment);
            if (await _paymentService.VoidForCancelledSessionAsync(payment.Id))
                result.PaymentsVoided++;
        }

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Completed))
        {
            if (!request.RefundPaidToCredit)
            {
                result.PaidNotRefunded++;
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
            }
            catch (Exception ex) when (ex is ValidationException or NotFoundException)
            {
                _logger.LogWarning(ex, "Could not refund payment {PaymentId} for cancelled session {SessionId}", payment.Id, sessionId);
                result.PaidNotRefunded++;
            }
        }

        var players = registrations
            .Select(r => r.User)
            .Where(u => u is { IsDeactivated: false })
            .DistinctBy(u => u.Id)
            .ToList();
        result.PlayersNotified = players.Count;

        try
        {
            await _email.SendSessionCancellationEmailAsync(session, players.Where(u => u.EmailNotificationsEnabled).ToList(), reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancellation emails failed for session {SessionId}", sessionId);
        }

        foreach (var player in players)
        {
            try
            {
                await _notifications.CreateAsync(player.Id, NotificationType.SessionCancelled,
                    title: "Session cancelled",
                    body: $"The {session.SessionDate:MMMM d} session is cancelled." + (reason == null ? "" : $" {reason}"),
                    url: "/schedule");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not notify player {UserId} about cancelled session {SessionId}", player.Id, sessionId);
            }
        }

        return result;
    }

    /// An unpaid card checkout would otherwise still accept money for a session that no longer happens.
    private async Task CloseOpenCardCheckoutAsync(Payment payment)
    {
        if (payment.Reference?.StartsWith("cs_", StringComparison.Ordinal) != true) return;
        try
        {
            await _stripe.ExpireCheckoutAsync(payment.Reference);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not expire Stripe checkout for payment {PaymentId}", payment.Id);
        }
    }
}
