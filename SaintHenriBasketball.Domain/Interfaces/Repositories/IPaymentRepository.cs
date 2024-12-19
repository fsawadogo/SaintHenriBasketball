using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IPaymentRepository : IGenericRepository<Payment>
{
    Task<IReadOnlyList<Payment>> GetPaymentsByPlayerAsync(Guid playerId);
    Task<IReadOnlyList<Payment>> GetPaymentsBySessionAsync(Guid sessionId);
}
