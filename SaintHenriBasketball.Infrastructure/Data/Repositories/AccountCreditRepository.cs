using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class AccountCreditRepository : IAccountCreditRepository
{
    private readonly ApplicationDbContext _context;

    public AccountCreditRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetBalanceAsync(Guid userId) =>
        await _context.AccountCredits.Where(c => c.UserId == userId).SumAsync(c => (decimal?)c.Amount) ?? 0m;

    public async Task<IReadOnlyList<AccountCredit>> GetByUserAsync(Guid userId) =>
        await _context.AccountCredits
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<bool> TryAddAsync(AccountCredit credit)
    {
        _context.AccountCredits.Add(credit);
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (DbUpdateExceptions.IsUniqueViolation(ex))
        {
            DbUpdateExceptions.Detach(_context, credit);
            return false;
        }
    }
}
