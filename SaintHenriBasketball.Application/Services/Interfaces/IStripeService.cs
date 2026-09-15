namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(Guid userId, Guid sessionId, Guid paymentId);
    Task<string> CreateSeasonCheckoutSessionAsync(Guid userId, Guid seasonId, Guid paymentId);
    /// Refunds the card charge behind a Checkout Session. Idempotent per payment; returns the Stripe refund id.
    Task<string> RefundCheckoutAsync(string checkoutSessionId, long amountInCents, Guid paymentId);
    /// Closes an unpaid Checkout Session so it can no longer be paid. Does nothing if it's already complete or expired.
    Task ExpireCheckoutAsync(string checkoutSessionId);
}
