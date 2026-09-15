using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class PaymentRefundService : IPaymentRefundService
{
    public const int MaxReasonLength = 500;
    public const string NotCardPaymentMessage = "This payment wasn't made by card. Refund it as account credit, or record that the club sent the money back.";
    public const string NotCompletedMessage = "Only a completed payment can be refunded.";
    public const string ReasonRequiredMessage = "Add a reason for the refund.";

    private readonly IPaymentRepository _payments;
    private readonly IAccountCreditRepository _credits;
    private readonly IStripeService _stripe;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentRefundService> _logger;

    public PaymentRefundService(
        IPaymentRepository payments,
        IAccountCreditRepository credits,
        IStripeService stripe,
        IPaymentService paymentService,
        ILogger<PaymentRefundService> logger)
    {
        _payments = payments;
        _credits = credits;
        _stripe = stripe;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<PaymentDto> RefundAsync(Guid paymentId, RefundPaymentDto request)
    {
        var payment = await _payments.GetByIdAsync(paymentId) ?? throw new NotFoundException($"Payment with ID {paymentId} not found");
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new ValidationException(ReasonRequiredMessage);
        if (reason.Length > MaxReasonLength)
            throw new ValidationException($"Keep the refund reason under {MaxReasonLength} characters.");
        if (!Enum.IsDefined(request.Method))
            throw new ValidationException("Choose how the money goes back to the player.");
        if (payment.Status == PaymentStatus.Refunded)
            return await _paymentService.GetPaymentAsync(paymentId);
        if (payment.Status != PaymentStatus.Completed)
            throw new ValidationException(NotCompletedMessage);

        // Money moves first; both paths are idempotent (Stripe idempotency key, one Refund credit row per payment),
        // so a retry after a later failure never pays the player twice.
        switch (request.Method)
        {
            case RefundMethod.Card:
                if (payment.Reference?.StartsWith("cs_", StringComparison.Ordinal) != true)
                    throw new ValidationException(NotCardPaymentMessage);
                if (payment.Amount > 0m)
                    await _stripe.RefundCheckoutAsync(payment.Reference, StripeCheckoutMatch.ToCents(payment.Amount), payment.Id);
                break;
            case RefundMethod.AccountCredit:
                if (payment.Amount > 0m)
                    await _credits.TryAddAsync(new AccountCredit(payment.UserId, payment.Amount, AccountCreditKind.Refund, paymentId: payment.Id, note: reason));
                break;
            case RefundMethod.Manual:
                break;
        }

        payment.RefundMethod = request.Method;
        payment.RefundReason = reason;
        payment.RefundedOn = DateTime.UtcNow;
        await _payments.UpdateAsync(payment);
        _logger.LogInformation("Payment {PaymentId} refunded ({Method})", payment.Id, request.Method);

        return await _paymentService.UpdatePaymentStatusAsync(paymentId, PaymentStatus.Refunded);
    }
}
