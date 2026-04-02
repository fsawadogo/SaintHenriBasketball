using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Settings;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Stripe;
using StripeCheckout = Stripe.Checkout;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class StripeService : IStripeService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly StripeSettings _stripeSettings;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IPaymentRepository paymentRepository,
        IOptions<StripeSettings> stripeSettings,
        ILogger<StripeService> logger)
    {
        _sessionRepository = sessionRepository;
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

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new NotFoundException($"User {userId} not found");

        var payment = await _paymentRepository.GetByIdAsync(paymentId);
        if (payment == null)
            throw new NotFoundException($"Payment {paymentId} not found");

        var amount = session.DropInPrice > 0 ? session.DropInPrice : 10m;
        var amountInCents = (long)(amount * 100);

        var sessionDate = session.SessionDate.ToString("MMM d, yyyy");
        var successUrl = _stripeSettings.SuccessUrl.Replace("{SESSION_ID}", sessionId.ToString());
        var cancelUrl = _stripeSettings.CancelUrl.Replace("{SESSION_ID}", sessionId.ToString());

        var options = new StripeCheckout.SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "payment",
            CustomerEmail = user.Email,
            LineItems = new List<StripeCheckout.SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new StripeCheckout.SessionLineItemPriceDataOptions
                    {
                        Currency = "cad",
                        UnitAmount = amountInCents,
                        ProductData = new StripeCheckout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Drop-In Basketball - {sessionDate}",
                            Description = $"{session.StartTime} - {session.EndTime} at {session.Location}",
                        },
                    },
                    Quantity = 1,
                },
            },
            Metadata = new Dictionary<string, string>
            {
                { "paymentId", paymentId.ToString() },
                { "userId", userId.ToString() },
                { "sessionId", sessionId.ToString() },
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        };

        var service = new StripeCheckout.SessionService();
        var checkoutSession = await service.CreateAsync(options);

        // Store the Stripe Checkout Session ID in the payment reference
        payment.Reference = checkoutSession.Id;
        await _paymentRepository.UpdateAsync(payment);

        _logger.LogInformation(
            "Stripe Checkout Session {CheckoutSessionId} created for payment {PaymentId}, user {UserId}",
            checkoutSession.Id, paymentId, userId);

        return checkoutSession.Url;
    }
}
