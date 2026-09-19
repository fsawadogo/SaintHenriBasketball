using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Settings;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Stripe;
using StripeCheckout = Stripe.Checkout;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class StripeService : IStripeService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly StripeSettings _stripeSettings;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        ISessionRepository sessionRepository,
        ISeasonRepository seasonRepository,
        IUserRepository userRepository,
        IPaymentRepository paymentRepository,
        IOptions<StripeSettings> stripeSettings,
        ILogger<StripeService> logger)
    {
        _sessionRepository = sessionRepository;
        _seasonRepository = seasonRepository;
        _userRepository = userRepository;
        _paymentRepository = paymentRepository;
        _stripeSettings = stripeSettings.Value;
        _logger = logger;
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid userId, Guid sessionId, Guid paymentId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
            throw new NotFoundException($"Session {sessionId} not found");

        var sessionDate = session.SessionDate.ToString("MMM d, yyyy");
        return await CreateAsync(userId, paymentId,
            name: $"Drop-In Basketball - {sessionDate}",
            description: $"{session.StartTime} - {session.EndTime} at {session.Location}",
            metadataKey: "sessionId", metadataValue: sessionId,
            successUrl: _stripeSettings.SuccessUrl.Replace("{SESSION_ID}", sessionId.ToString()),
            cancelUrl: _stripeSettings.CancelUrl.Replace("{SESSION_ID}", sessionId.ToString()),
            idempotencyPrefix: "drop-in");
    }

    public async Task<string> CreateSeasonCheckoutSessionAsync(Guid userId, Guid seasonId, Guid paymentId)
    {
        var season = await _seasonRepository.GetByIdAsync(seasonId);
        if (season == null)
            throw new NotFoundException($"Season {seasonId} not found");

        var seasonName = string.IsNullOrWhiteSpace(season.Name) ? "Season" : season.Name;
        return await CreateAsync(userId, paymentId,
            name: $"Season Pass - {seasonName}",
            description: $"{season.StartDate:MMM d, yyyy} - {season.EndDate:MMM d, yyyy}",
            metadataKey: "seasonId", metadataValue: seasonId,
            successUrl: _stripeSettings.SeasonSuccessUrl.Replace("{SEASON_ID}", seasonId.ToString()),
            cancelUrl: _stripeSettings.SeasonCancelUrl.Replace("{SEASON_ID}", seasonId.ToString()),
            idempotencyPrefix: "season");
    }

    private async Task<string> CreateAsync(Guid userId, Guid paymentId, string name, string description,
        string metadataKey, Guid metadataValue, string successUrl, string cancelUrl, string idempotencyPrefix)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new NotFoundException($"User {userId} not found");

        var payment = await _paymentRepository.GetByIdAsync(paymentId);
        if (payment == null)
            throw new NotFoundException($"Payment {paymentId} not found");
        if (payment.UserId != userId || !BelongsTo(payment, metadataKey, metadataValue))
            throw new ValidationException("This payment does not match the checkout request.");

        var options = new StripeCheckout.SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = user.Email,
            LineItems = new List<StripeCheckout.SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new StripeCheckout.SessionLineItemPriceDataOptions
                    {
                        Currency = "cad",
                        UnitAmount = StripeCheckoutMatch.ToCents(payment.Amount),
                        ProductData = new StripeCheckout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = name,
                            Description = description,
                        },
                    },
                    Quantity = 1,
                },
            },
            Metadata = new Dictionary<string, string>
            {
                { "paymentId", paymentId.ToString() },
                { "userId", userId.ToString() },
                { metadataKey, metadataValue.ToString() },
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        };

        var service = new StripeCheckout.SessionService();
        var checkoutSession = await service.CreateAsync(options, new RequestOptions { IdempotencyKey = $"{idempotencyPrefix}-{payment.Id}" });

        // Store the Stripe Checkout Session ID in the payment reference
        if (!await _paymentRepository.TrySetPendingReferenceAsync(payment.Id, payment.Reference, checkoutSession.Id))
            throw new ValidationException("The payment changed. Review your payment history before continuing.");

        _logger.LogInformation(
            "Stripe Checkout Session {CheckoutSessionId} created for payment {PaymentId}, user {UserId}",
            checkoutSession.Id, paymentId, userId);

        return checkoutSession.Url;
    }

    public async Task<bool> ExpireCheckoutAsync(string checkoutSessionId)
    {
        var service = new StripeCheckout.SessionService();
        var checkout = await service.GetAsync(checkoutSessionId);

        // "complete" covers both a card already charged and an async method (bank debit) still
        // settling — its webhook can land days later. Either way money is on its way to us, and
        // writing the payment off would leave the club holding it with no record.
        if (checkout.Status == "complete")
        {
            _logger.LogWarning(
                "Stripe Checkout Session {CheckoutSessionId} is already complete; leaving the payment alone",
                checkoutSessionId);
            return false;
        }

        if (checkout.Status == "open")
        {
            await service.ExpireAsync(checkoutSessionId);
            _logger.LogInformation("Stripe Checkout Session {CheckoutSessionId} expired", checkoutSessionId);
        }

        // Open-and-now-expired, or already expired: nothing can arrive through it.
        return true;
    }

    public async Task<string> RefundCheckoutAsync(string checkoutSessionId, long amountInCents, Guid paymentId)
    {
        var checkout = await new StripeCheckout.SessionService().GetAsync(checkoutSessionId);
        if (string.IsNullOrEmpty(checkout.PaymentIntentId))
            throw new ValidationException("Stripe has no completed charge for this checkout, so it can't be refunded to the card.");

        var refund = await new RefundService().CreateAsync(
            new RefundCreateOptions
            {
                PaymentIntent = checkout.PaymentIntentId,
                Amount = amountInCents,
                Reason = "requested_by_customer",
                Metadata = new Dictionary<string, string> { { "paymentId", paymentId.ToString() } },
            },
            new RequestOptions { IdempotencyKey = $"refund-{paymentId}" });

        _logger.LogInformation("Stripe refund {RefundId} created for payment {PaymentId}", refund.Id, paymentId);
        return refund.Id;
    }

    private static bool BelongsTo(Payment payment, string metadataKey, Guid id) => metadataKey == "seasonId"
        ? payment.Plan == PaymentPlan.Season && payment.SeasonId == id
        : payment.SessionId == id;
}
