using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Settings;
using SaintHenriBasketball.Domain.Enums;
using Stripe;
using StripeCheckout = Stripe.Checkout;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stripe")]
[ApiController]
public class StripeWebhookController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ICacheService _cacheService;
    private readonly StripeSettings _stripeSettings;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IPaymentService paymentService,
        ICacheService cacheService,
        IOptions<StripeSettings> stripeSettings,
        ILogger<StripeWebhookController> logger)
    {
        _paymentService = paymentService;
        _cacheService = cacheService;
        _stripeSettings = stripeSettings.Value;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _stripeSettings.WebhookSecret);

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as StripeCheckout.Session;
                if (session == null)
                {
                    _logger.LogWarning("Stripe webhook: checkout.session.completed but session object was null");
                    return Ok();
                }

                await HandleCheckoutCompleted(session);
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest("Webhook signature verification failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            return StatusCode(500);
        }
    }

    private async Task HandleCheckoutCompleted(StripeCheckout.Session session)
    {
        if (!session.Metadata.TryGetValue("paymentId", out var paymentIdStr) ||
            !Guid.TryParse(paymentIdStr, out var paymentId))
        {
            _logger.LogWarning("Stripe webhook: missing or invalid paymentId in metadata");
            return;
        }

        try
        {
            var payment = await _paymentService.GetPaymentAsync(paymentId);

            // Idempotency: skip if already completed
            if (payment.Status == PaymentStatus.Completed)
            {
                _logger.LogInformation("Payment {PaymentId} already completed, skipping webhook", paymentId);
                return;
            }

            await _paymentService.UpdatePaymentStatusAsync(paymentId, PaymentStatus.Completed);

            // Invalidate caches
            await _cacheService.RemoveAsync($"Payments:Detail:{paymentId}");
            await _cacheService.RemoveAsync($"Payments:User:{payment.UserId}");
            await _cacheService.RemoveAsync("Payments:Pending");
            await _cacheService.RemoveAsync("Payments:Summary");

            _logger.LogInformation(
                "Payment {PaymentId} marked as Completed via Stripe webhook (session {StripeSessionId})",
                paymentId, session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling checkout.session.completed for payment {PaymentId}", paymentId);
        }
    }
}
