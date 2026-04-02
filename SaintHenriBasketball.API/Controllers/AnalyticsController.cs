using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AnalyticsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Revenue breakdown by month, plan type</summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string groupBy = "month")
    {
        var startDate = from ?? DateTime.UtcNow.AddMonths(-12);
        var endDate = to ?? DateTime.UtcNow;

        var payments = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaymentDate >= startDate && p.PaymentDate <= endDate)
            .ToListAsync();

        var totalRevenue = payments.Sum(p => p.Amount);
        var seasonRevenue = payments.Where(p => p.Plan == PaymentPlan.Season).Sum(p => p.Amount);
        var dropInRevenue = payments.Where(p => p.Plan == PaymentPlan.DropIn).Sum(p => p.Amount);

        var monthly = payments
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new {
                period = $"{g.Key.Year}-{g.Key.Month:D2}",
                total = g.Sum(p => p.Amount),
                season = g.Where(p => p.Plan == PaymentPlan.Season).Sum(p => p.Amount),
                dropIn = g.Where(p => p.Plan == PaymentPlan.DropIn).Sum(p => p.Amount),
                count = g.Count()
            });

        var byStatus = await _db.Payments
            .Where(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate)
            .GroupBy(p => p.Status)
            .Select(g => new { status = g.Key.ToString(), count = g.Count(), amount = g.Sum(p => p.Amount) })
            .ToListAsync();

        return Ok(new { totalRevenue, seasonRevenue, dropInRevenue, monthly, byStatus });
    }

    /// <summary>Player engagement tiers based on attendance</summary>
    [HttpGet("engagement")]
    public async Task<IActionResult> GetPlayerEngagement()
    {
        var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var users = await _db.Users.Where(u => !u.IsAdmin).ToListAsync();
        var recentAttendance = await _db.SessionAttendances
            .Where(a => a.CreatedOn >= sixtyDaysAgo)
            .GroupBy(a => a.UserId)
            .Select(g => new {
                userId = g.Key,
                total = g.Count(),
                attended = g.Count(a => a.IsAttending),
                lastActive = g.Max(a => a.CreatedOn)
            })
            .ToListAsync();

        var totalSessionsInPeriod = await _db.Sessions
            .Where(s => s.SessionDate >= sixtyDaysAgo && s.SessionDate <= DateTime.UtcNow)
            .CountAsync();

        var engagement = users.Select(u =>
        {
            var stats = recentAttendance.FirstOrDefault(a => a.userId == u.Id);
            var rate = stats != null && totalSessionsInPeriod > 0
                ? (double)stats.attended / totalSessionsInPeriod * 100
                : 0;
            var tier = rate >= 80 ? "High" : rate >= 50 ? "Medium" : rate >= 20 ? "Low" : "Inactive";
            var isInactive = stats == null || stats.lastActive < thirtyDaysAgo;

            return new {
                userId = u.Id,
                name = $"{u.FirstName} {u.LastName}",
                email = u.Email,
                attendanceRate = Math.Round(rate, 1),
                sessionsAttended = stats?.attended ?? 0,
                tier,
                isInactive,
                lastActive = stats?.lastActive
            };
        }).OrderByDescending(e => e.attendanceRate);

        var summary = new {
            high = engagement.Count(e => e.tier == "High"),
            medium = engagement.Count(e => e.tier == "Medium"),
            low = engagement.Count(e => e.tier == "Low"),
            inactive = engagement.Count(e => e.tier == "Inactive")
        };

        return Ok(new { players = engagement, summary });
    }

    /// <summary>Session popularity by day of week</summary>
    [HttpGet("popularity")]
    public async Task<IActionResult> GetSessionPopularity()
    {
        var sessions = await _db.Sessions
            .Where(s => s.Status != SessionStatus.Cancelled)
            .Select(s => new {
                dayOfWeek = s.SessionDate.DayOfWeek,
                startTime = s.StartTime,
                registrations = s.RegisteredPlayersCount,
                capacity = s.MaxCapacity
            })
            .ToListAsync();

        var byDay = sessions
            .GroupBy(s => s.dayOfWeek)
            .Select(g => new {
                day = g.Key.ToString(),
                avgRegistrations = Math.Round(g.Average(s => s.registrations), 1),
                avgCapacityPct = g.Average(s => s.capacity > 0 ? (double)s.registrations / s.capacity * 100 : 0),
                sessionCount = g.Count()
            })
            .OrderBy(d => d.day);

        return Ok(byDay);
    }
}
