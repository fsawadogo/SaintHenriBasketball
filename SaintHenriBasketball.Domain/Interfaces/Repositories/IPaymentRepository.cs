using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IPaymentRepository
{
    Task<IReadOnlyList<Payment>> GetPaymentsByUserAsync(Guid userId);
    Task<Payment> GetByIdAsync(Guid id);
    Task AddAsync(Payment payment);
    Task UpdateAsync(Payment payment);
}
