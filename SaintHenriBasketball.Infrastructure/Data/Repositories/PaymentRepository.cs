using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsByPlayerAsync(Guid playerId)
    {
        return await _context.Payments
            .Include(p => p.Session)
            .Where(p => p.PlayerId == playerId)
            .OrderByDescending(p => p.CreatedOn)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsBySessionAsync(Guid sessionId)
    {
        return await _context.Payments
            .Include(p => p.Player)
            .Where(p => p.SessionId == sessionId)
            .OrderByDescending(p => p.CreatedOn)
            .ToListAsync();
    }
}
