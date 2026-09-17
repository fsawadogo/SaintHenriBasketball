using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class InteracDepositRepository(ApplicationDbContext db) : IInteracDepositRepository
{
    public async Task<InteracDeposit?> GetAsync(Guid id) =>
        await db.InteracDeposits.SingleOrDefaultAsync(d => d.Id == id);

    public async Task<InteracDeposit?> FindDuplicateAsync(string fingerprint, string? messageId) =>
        await db.InteracDeposits
            .Where(d => d.Fingerprint == fingerprint || (messageId != null && d.MessageId == messageId))
            .FirstOrDefaultAsync();

    public async Task AddAsync(InteracDeposit deposit) => await db.InteracDeposits.AddAsync(deposit);

    public async Task SaveAsync() => await db.SaveChangesAsync();

    public async Task<IReadOnlyList<InteracDeposit>> ListAsync(bool unmatchedOnly, int limit) =>
        await db.InteracDeposits.AsNoTracking()
            .Where(d => !unmatchedOnly || d.Status == InteracDepositStatus.Unmatched)
            .OrderByDescending(d => d.ReceivedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<IReadOnlyList<PendingPaymentCandidate>> GetPendingCandidatesAsync(DateTime createdAfterUtc) =>
        (await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Pending && p.CreatedAt >= createdAfterUtc)
            .Join(db.Users.AsNoTracking(), p => p.UserId, u => u.Id, (p, u) => new { Payment = p, Name = u.FirstName + " " + u.LastName })
            .ToListAsync())
        .Select(x => new PendingPaymentCandidate(x.Payment, x.Name))
        .ToList();

    public async Task<string?> GetPlayerNameAsync(Guid paymentId) =>
        await db.Payments.AsNoTracking()
            .Where(p => p.Id == paymentId)
            .Join(db.Users.AsNoTracking(), p => p.UserId, u => u.Id, (p, u) => u.FirstName + " " + u.LastName)
            .SingleOrDefaultAsync();
}
