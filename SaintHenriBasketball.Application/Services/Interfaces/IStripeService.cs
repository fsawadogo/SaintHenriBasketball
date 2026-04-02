namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(Guid userId, Guid sessionId, Guid paymentId);
}
