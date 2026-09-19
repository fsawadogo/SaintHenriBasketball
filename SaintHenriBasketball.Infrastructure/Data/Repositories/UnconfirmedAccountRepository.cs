using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class UnconfirmedAccountRepository : IUnconfirmedAccountRepository
{
    private readonly ApplicationDbContext _context;

    public UnconfirmedAccountRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PurgeableAccount>> GetPurgeableAsync(DateTime cutoff, DateTime? createdAfter = null)
    {
        return await _context.Users.AsNoTracking()
            .Where(u => !u.EmailConfirmed && !u.IsAdmin && u.CreatedOn < cutoff)
            .Where(u => createdAfter == null || u.CreatedOn >= createdAfter)
            .OrderBy(u => u.CreatedOn)
            .Select(u => new PurgeableAccount(
                u.Id,
                ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
                u.Email,
                u.CreatedOn,
                // Anything the club would lose by removing this row. A real person whose
                // confirmation mail went astray may well have none of these, which is why the
                // decision is shown before it is made rather than after.
                _context.Payments.Any(p => p.UserId == u.Id)
                    || _context.SessionRegistrations.Any(r => r.UserId == u.Id)
                    || _context.SessionAttendances.Any(a => a.UserId == u.Id)
                    || _context.Waitlists.Any(w => w.UserId == u.Id)))
            .ToListAsync();
    }

    public async Task DeleteAsync(Guid userId)
    {
        await _context.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
    }
}
