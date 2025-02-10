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
}