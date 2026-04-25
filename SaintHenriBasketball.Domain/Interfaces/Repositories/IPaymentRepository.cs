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
    Task<IEnumerable<Payment>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// Returns the most recent non-Refunded payment for the (user, session) pair, or null.
    /// Used by the auto-billing path to short-circuit when a Pending or Completed payment
    /// already exists.
    Task<Payment?> GetByUserAndSessionAsync(Guid userId, Guid sessionId);
}
