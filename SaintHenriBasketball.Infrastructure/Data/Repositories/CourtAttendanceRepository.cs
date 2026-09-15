using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class CourtAttendanceRepository(ApplicationDbContext db) : ICourtAttendanceRepository
{
    public async Task<IReadOnlyList<CourtRosterEntry>> GetRosterAsync(Guid sessionId)
    {
        var registered = await db.SessionRegistrations.AsNoTracking()
            .Where(r => r.SessionId == sessionId)
            .Select(r => r.UserId)
            .ToListAsync();
        var answers = (await db.SessionAttendances.AsNoTracking()
                .Where(a => a.SessionId == sessionId)
                .Select(a => new { a.UserId, a.IsAttending, a.Outcome, a.CheckInTime })
                .ToListAsync())
            .ToDictionary(a => a.UserId);

        var userIds = registered
            .Concat(answers.Values.Where(a => a.IsAttending || a.Outcome != AttendanceOutcome.Unmarked).Select(a => a.UserId))
            .Distinct()
            .ToList();
        if (userIds.Count == 0) return Array.Empty<CourtRosterEntry>();

        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.PaymentPlan, u.IsDeactivated })
            .ToListAsync();

        return users
            .Select(u =>
            {
                var answer = answers.GetValueOrDefault(u.Id);
                return new CourtRosterEntry(u.Id, u.FirstName, u.LastName, u.Email, u.PaymentPlan, u.IsDeactivated,
                    answer?.IsAttending, answer?.Outcome ?? AttendanceOutcome.Unmarked,
                    answer?.CheckInTime is DateTime checkIn ? DateTime.SpecifyKind(checkIn, DateTimeKind.Utc) : null);
            })
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .ToList();
    }

    public async Task<IReadOnlyList<CourtOutcomeRow>> GetOutcomeRowsAsync(DateTime sessionDateFrom, DateTime sessionDateTo, Guid? userId)
    {
        var from = sessionDateFrom.Date;
        var toExclusive = sessionDateTo.Date.AddDays(1);

        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.SessionDate >= from && s.SessionDate < toExclusive && s.Status != SessionStatus.Cancelled)
            .Select(s => new { s.Id, s.SessionDate, s.StartTime, s.EndTime })
            .ToDictionaryAsync(s => s.Id);
        if (sessions.Count == 0) return Array.Empty<CourtOutcomeRow>();

        var registrations = db.SessionRegistrations.AsNoTracking()
            .Where(r => r.Session.SessionDate >= from && r.Session.SessionDate < toExclusive && r.Session.Status != SessionStatus.Cancelled);
        var attendances = db.SessionAttendances.AsNoTracking()
            .Where(a => a.Session.SessionDate >= from && a.Session.SessionDate < toExclusive && a.Session.Status != SessionStatus.Cancelled);
        if (userId is Guid onlyUser)
        {
            registrations = registrations.Where(r => r.UserId == onlyUser);
            attendances = attendances.Where(a => a.UserId == onlyUser);
        }

        var registered = await registrations.Select(r => new { r.UserId, r.SessionId }).ToListAsync();
        var answers = (await attendances.Select(a => new { a.UserId, a.SessionId, a.IsAttending, a.Outcome }).ToListAsync())
            .ToDictionary(a => (a.UserId, a.SessionId));

        var places = registered.Select(r => (r.UserId, r.SessionId))
            .Concat(answers.Values.Where(a => a.IsAttending || a.Outcome != AttendanceOutcome.Unmarked).Select(a => (a.UserId, a.SessionId)))
            .Where(p => sessions.ContainsKey(p.SessionId))
            .Distinct()
            .ToList();
        if (places.Count == 0) return Array.Empty<CourtOutcomeRow>();

        var userIds = places.Select(p => p.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.IsDeactivated })
            .ToDictionaryAsync(u => u.Id);

        return places
            .Where(p => users.ContainsKey(p.UserId))
            .Select(p =>
            {
                var user = users[p.UserId];
                var session = sessions[p.SessionId];
                var outcome = answers.GetValueOrDefault(p)?.Outcome ?? AttendanceOutcome.Unmarked;
                return new CourtOutcomeRow(user.Id, user.FirstName, user.LastName, user.Email, user.IsDeactivated,
                    session.Id, session.SessionDate, session.StartTime, session.EndTime, outcome);
            })
            .ToList();
    }
}
