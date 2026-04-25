using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly ApplicationDbContext _context;

    public PaymentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsByUserAsync(Guid userId)
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Session)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<Payment> GetByIdAsync(Guid id)
    {
        return (await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Session)
            .FirstOrDefaultAsync(p => p.Id == id))!;
    }

    // Session must be Included on every read path: PaymentDto.SessionDate is mapped from
    // Payment.Session.SessionDate, and the payment-reminder cron's `(today - sessionDate)`
    // modulus filter silently excludes every row when Session is null.
    public async Task<IReadOnlyList<Payment>> GetAllAsync()
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Session)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsByStatusAsync(PaymentStatus status)
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Session)
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsByTypeAsync(PaymentPlan plan)
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Session)
            .Where(p => p.Plan == plan)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task AddAsync(Payment payment)
    {
        await _context.Payments.AddAsync(payment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Payment payment)
    {
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync();
    }
    
    public async Task<IEnumerable<Payment>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Payments
            .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
            .Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Payment?> GetByUserAndSessionAsync(Guid userId, Guid sessionId)
    {
        return await _context.Payments
            .Where(p => p.UserId == userId && p.SessionId == sessionId && p.Status != PaymentStatus.Refunded)
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefaultAsync();
    }
}