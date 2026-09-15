using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class VolunteerRolesRepository(ApplicationDbContext db) : IVolunteerRolesRepository
{
    public Task<StaffRoleTarget?> GetStaffRoleTargetAsync(Guid userId) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new StaffRoleTarget(u.Id, u.FirstName, u.LastName, u.IsAdmin, u.IsDeactivated, u.StaffRole))
            .FirstOrDefaultAsync();

    public Task SetStaffRoleAsync(Guid userId, StaffRole role) =>
        db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.StaffRole, role));

    public async Task<IReadOnlyList<CourtSessionRow>> GetCourtSessionsAsync(DateTime sessionDateFrom, DateTime sessionDateTo)
    {
        var from = sessionDateFrom.Date;
        var toExclusive = sessionDateTo.Date.AddDays(1);
        return await db.Sessions.AsNoTracking()
            .Where(s => s.SessionDate >= from && s.SessionDate < toExclusive && s.Status != SessionStatus.Cancelled)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
            .Select(s => new CourtSessionRow(s.Id, s.SessionDate, s.StartTime, s.EndTime, s.Location, s.Status, s.RegisteredPlayersCount, s.MaxCapacity))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<CourtPlayerRow>> SearchActivePlayersAsync(string term, int max) =>
        await db.Users.AsNoTracking()
            .Where(u => !u.IsDeactivated)
            .Where(u => (u.Email != null && u.Email.Contains(term))
                || (u.Username != null && u.Username.Contains(term))
                || ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Contains(term))
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Take(max)
            .Select(u => new CourtPlayerRow(u.Id, u.FirstName, u.LastName, u.Email))
            .ToListAsync();
}
