using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto createPaymentDto);
    Task<PaymentDto> GetPaymentAsync(Guid id);
    Task<IEnumerable<PaymentDto>> GetUserPaymentsAsync(Guid userId);
    Task<PaymentDto> UpdatePaymentStatusAsync(Guid id, PaymentStatus status);
    Task<PaymentSummaryDto> GetPaymentSummaryAsync();
    Task<IEnumerable<PaymentDto>> GetPendingPaymentsAsync();
    Task<IEnumerable<PaymentDto>> GetAllPayments();
    Task<PaymentDto> ProcessPaymentAsync(CreatePaymentDto createPaymentDto);
    Task<PaymentReconciliationDto> ReconcilePaymentsAsync(DateTime startDate, DateTime endDate);
    Task<PaymentDto> UpdatePaymentAsync(Guid id, UpdatePaymentDto updatePaymentDto);
    Task<PaymentDto> CreateDropInPaymentAsync(Guid userId, CreateDropInPaymentDto request);
    Task<PaymentDto> ConfirmInteracPaymentAsync(Guid paymentId, string reference);
    Task<DropInPaymentLinkDto> GetDropInPaymentLinkAsync(Guid userId, Guid sessionId);

    /// Idempotently creates a Pending drop-in payment for the (user, session) pair if one
    /// doesn't already exist. Returns the payment Id paired with whether it was newly
    /// created, or null when the user is on Season plan / not registered for the
    /// session / the session isn't today.
    Task<(Guid Id, bool Created)?> EnsureDropInPaymentForSessionAsync(Guid userId, Guid sessionId);

    /// Sweeps every open session whose date is today and ensures a Pending drop-in payment
    /// for each registered drop-in player. Idempotent — safe to combine with QR check-in.
    /// Returns the number of new payments created.
    Task<int> RunDailyDropInBillingAsync();
}