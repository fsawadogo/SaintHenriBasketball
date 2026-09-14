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

    public async Task<(Payment Payment, bool Created)> GetOrCreateSessionPaymentAsync(Guid userId, Guid sessionId, decimal amount)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        // Serialize billing and user checkout for this session, across API instances.
        await _context.Sessions.FromSqlInterpolated($"SELECT * FROM Sessions WITH (UPDLOCK, HOLDLOCK) WHERE Id = {sessionId}").SingleAsync();
        var existing = await GetByUserAndSessionAsync(userId, sessionId);
        if (existing != null) { await transaction.CommitAsync(); return (existing, false); }
        var payment = new Payment(userId, amount, PaymentPlan.DropIn, sessionId) {
            Reference = $"DROPIN-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow
        };
        await AddAsync(payment);
        await transaction.CommitAsync();
        return (payment, true);
    }

    public async Task<(Payment Payment, bool Created)> GetOrCreateSeasonPaymentAsync(Guid userId, Guid seasonId, decimal amount)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Seasons.FromSqlInterpolated($"SELECT * FROM Seasons WITH (UPDLOCK, HOLDLOCK) WHERE Id = {seasonId}").SingleAsync();
        var existing = await _context.Payments
            .Where(p => p.UserId == userId && p.SeasonId == seasonId && p.Status != PaymentStatus.Refunded && p.Status != PaymentStatus.Failed)
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefaultAsync();
        if (existing != null) { await transaction.CommitAsync(); return (existing, false); }
        var payment = new Payment(userId, amount, PaymentPlan.Season)
        {
            SeasonId = seasonId,
            Reference = $"SEASON-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };
        await AddAsync(payment);
        await transaction.CommitAsync();
        return (payment, true);
    }

    public async Task AddAsync(Payment payment)
    {
        await _context.Payments.AddAsync(payment);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> TrySetPendingReferenceAsync(Guid id, string? expectedReference, string reference)
    {
        return await _context.Payments.Where(p => p.Id == id && p.Status == PaymentStatus.Pending && p.Reference == expectedReference)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.Reference, reference)) == 1;
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
