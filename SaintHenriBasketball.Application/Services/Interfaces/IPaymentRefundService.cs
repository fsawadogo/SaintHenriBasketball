using SaintHenriBasketball.Application.DTOs.Payment;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IPaymentRefundService
{
    /// <summary>
    /// Gives a completed payment's money back (card through Stripe, account credit, or recorded as sent
    /// back by hand), then marks it Refunded, which also releases any credit it used and notifies the player.
    /// Repeating the call for a payment that is already refunded changes nothing.
    /// </summary>
    Task<PaymentDto> RefundAsync(Guid paymentId, RefundPaymentDto request);
}
