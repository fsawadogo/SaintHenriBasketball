using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IPaymentRepository
{
    Task<IReadOnlyList<Payment>> GetPaymentsByUserAsync(Guid userId);
    Task<Payment> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Payment>> GetAllAsync();
    Task<IReadOnlyList<Payment>> GetPaymentsByStatusAsync(PaymentStatus status);
    Task<IReadOnlyList<Payment>> GetPaymentsByTypeAsync(PaymentPlan plan);
    Task AddAsync(Payment payment);
    Task UpdateAsync(Payment payment);
}
