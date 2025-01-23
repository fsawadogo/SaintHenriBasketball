using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto createPaymentDto);
    Task<PaymentDto> GetPaymentAsync(Guid id);
    Task<IEnumerable<PaymentDto>> GetUserPaymentsAsync(Guid userId);
    Task UpdatePaymentStatusAsync(Guid id, PaymentStatus status);
    Task<PaymentSummaryDto> GetPaymentSummaryAsync();
    Task<IEnumerable<PaymentDto>> GetPendingPaymentsAsync();
    Task<IEnumerable<PaymentDto>> GetAllPayments();
    Task DeletePaymentAsync(Guid id);

}