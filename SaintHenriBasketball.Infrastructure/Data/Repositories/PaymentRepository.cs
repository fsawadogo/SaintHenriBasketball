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
            .Include(p => p.PromoCode)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Payment>> GetBySessionAsync(Guid sessionId) =>
        await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Session)
            .Where(p => p.SessionId == sessionId)
            .ToListAsync();

    public async Task<Payment> GetByIdAsync(Guid id)
    {
        return (await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Session)
            .Include(p => p.PromoCode)
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
            .Include(p => p.PromoCode)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsByStatusAsync(PaymentStatus status)
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Session)
            .Include(p => p.PromoCode)
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsByTypeAsync(PaymentPlan plan)
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Session)
            .Include(p => p.PromoCode)
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
        var existing = await GetByUserAndSeasonAsync(userId, seasonId);
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

    public async Task<Payment?> GetByUserAndSeasonAsync(Guid userId, Guid seasonId)
    {
        return await _context.Payments
            .Where(p => p.UserId == userId && p.SeasonId == seasonId && p.Status != PaymentStatus.Refunded && p.Status != PaymentStatus.Failed)
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasCompletedPaymentAsync(Guid userId) =>
        await _context.Payments.AnyAsync(p => p.UserId == userId && p.Status == PaymentStatus.Completed);

    public async Task<bool> HasEarlierPaidPaymentAsync(Guid userId, Guid paymentId, DateTime completedBefore) =>
        await _context.Payments.AnyAsync(p => p.UserId == userId && p.Id != paymentId
            && p.Status == PaymentStatus.Completed && p.Amount > 0 && p.PaymentDate < completedBefore);

    public async Task<PaymentAdjustmentResult> TryApplyAdjustmentsAsync(Payment payment, PaymentAdjustment adjustment)
    {
        var newCredit = adjustment.CreditApplied - payment.CreditApplied;
        await using var transaction = await _context.Database.BeginTransactionAsync();

        if (newCredit > 0)
        {
            // Every credit debit locks the player's row first, so two payments cannot spend the same balance.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT Id FROM Users WITH (UPDLOCK, HOLDLOCK) WHERE Id = {payment.UserId}");
            var balance = await _context.AccountCredits.Where(c => c.UserId == payment.UserId)
                .SumAsync(c => (decimal?)c.Amount) ?? 0m;
            if (balance < newCredit) return PaymentAdjustmentResult.PaymentChanged;
        }

        if (adjustment.ReservePromoCodeId is Guid promoCodeId)
        {
            var now = DateTime.UtcNow;
            var reserved = await _context.PromoCodes
                .Where(p => p.Id == promoCodeId && p.IsActive && p.ValidFrom <= now && p.ValidUntil >= now
                    && (p.MaxUses == null || p.TimesUsed < p.MaxUses))
                .ExecuteUpdateAsync(update => update.SetProperty(p => p.TimesUsed, p => p.TimesUsed + 1));
            if (reserved != 1) return PaymentAdjustmentResult.PromoUnavailable;
        }

        var expectedReference = payment.Reference;
        var expectedAmount = payment.Amount;
        var expectedOriginal = payment.OriginalAmount;
        var expectedDiscount = payment.DiscountAmount;
        var expectedCredit = payment.CreditApplied;
        var expectedPromo = payment.PromoCodeId;
        var updated = await _context.Payments
            .Where(p => p.Id == payment.Id && p.Status == PaymentStatus.Pending && p.Reference == expectedReference
                && p.Amount == expectedAmount && p.OriginalAmount == expectedOriginal && p.DiscountAmount == expectedDiscount
                && p.CreditApplied == expectedCredit && p.PromoCodeId == expectedPromo)
            .ExecuteUpdateAsync(update => update
                .SetProperty(p => p.OriginalAmount, adjustment.OriginalAmount)
                .SetProperty(p => p.DiscountAmount, adjustment.DiscountAmount)
                .SetProperty(p => p.CreditApplied, adjustment.CreditApplied)
                .SetProperty(p => p.PromoCodeId, adjustment.PromoCodeId)
                .SetProperty(p => p.Amount, adjustment.Amount));
        if (updated != 1) return PaymentAdjustmentResult.PaymentChanged;

        if (newCredit > 0)
        {
            var debit = new AccountCredit(payment.UserId, -newCredit, AccountCreditKind.AppliedToPayment, paymentId: payment.Id);
            _context.AccountCredits.Add(debit);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (DbUpdateExceptions.IsUniqueViolation(ex))
            {
                DbUpdateExceptions.Detach(_context, debit);
                return PaymentAdjustmentResult.PaymentChanged;
            }
        }

        await transaction.CommitAsync();

        // Keep the tracked instance in step with the row: later reads in this scope return it as-is,
        // and marking the values as original stops a later SaveChanges from rewriting them.
        var entry = _context.Entry(payment);
        void Sync<T>(System.Linq.Expressions.Expression<Func<Payment, T>> property, T value)
        {
            var member = entry.Property(property);
            member.CurrentValue = value;
            if (entry.State != EntityState.Detached) { member.OriginalValue = value; member.IsModified = false; }
        }
        Sync(p => p.OriginalAmount, adjustment.OriginalAmount);
        Sync(p => p.DiscountAmount, adjustment.DiscountAmount);
        Sync(p => p.CreditApplied, adjustment.CreditApplied);
        Sync(p => p.PromoCodeId, adjustment.PromoCodeId);
        Sync(p => p.Amount, adjustment.Amount);
        return PaymentAdjustmentResult.Applied;
    }
}
