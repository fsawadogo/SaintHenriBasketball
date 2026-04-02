using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Infrastructure.Data.Context;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext db, ILogger<AdminController> logger)
    {
        _db = db;
        _logger = logger;
    }

    #region Audit Log

    [HttpGet("audit-log")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action.Contains(action));
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    #endregion

    #region User Notes

    [HttpGet("users/{userId}/notes")]
    public async Task<IActionResult> GetUserNotes(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();
        return Ok(new { notes = user.AdminNotes });
    }

    [HttpPut("users/{userId}/notes")]
    public async Task<IActionResult> UpdateUserNotes(Guid userId, [FromBody] UpdateNotesDto dto)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.AdminNotes = dto.Notes;
        await _db.SaveChangesAsync();

        // Log the action
        _db.AuditLogs.Add(new AuditLog(
            "UpdateNotes", "User", userId,
            $"Notes updated for {user.FirstName} {user.LastName}",
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var adminId) ? adminId : null,
            User.Identity?.Name ?? "Admin"
        ));
        await _db.SaveChangesAsync();

        return Ok(new { notes = user.AdminNotes });
    }

    #endregion

    #region Bulk User Import

    [HttpPost("users/import")]
    public async Task<IActionResult> ImportUsers([FromBody] List<ImportUserDto> users)
    {
        var results = new { created = 0, skipped = 0, errors = new List<string>() };
        var created = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var dto in users)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.FirstName))
                {
                    errors.Add($"Row missing required fields: {dto.Email}");
                    continue;
                }

                var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
                if (exists)
                {
                    skipped++;
                    continue;
                }

                var username = dto.Email.Split('@')[0];
                if (await _db.Users.AnyAsync(u => u.Username == username))
                    username = $"{username}{Random.Shared.Next(1000, 9999)}";

                var user = new ApplicationUser(
                    username: username,
                    email: dto.Email,
                    passwordHash: BCrypt.Net.BCrypt.HashPassword("Temp1234!"),
                    firstName: dto.FirstName,
                    lastName: dto.LastName ?? "",
                    paymentPlan: dto.PaymentPlan ?? Domain.Enums.PaymentPlan.DropIn
                );
                user.EmailConfirmed = true;
                user.EmailConfirmationToken = "";

                _db.Users.Add(user);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"Error importing {dto.Email}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog(
            "BulkImport", "User", null,
            $"Imported {created} users, skipped {skipped}, {errors.Count} errors"
        ));
        await _db.SaveChangesAsync();

        return Ok(new { created, skipped, errors });
    }

    #endregion

    #region Session Templates (Recurring)

    [HttpGet("session-templates")]
    public async Task<IActionResult> GetSessionTemplates()
    {
        var templates = await _db.SessionTemplates.OrderBy(t => t.DayOfWeek).ToListAsync();
        return Ok(templates);
    }

    [HttpPost("session-templates")]
    public async Task<IActionResult> CreateSessionTemplate([FromBody] SessionTemplate template)
    {
        _db.SessionTemplates.Add(template);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSessionTemplates), new { id = template.Id }, template);
    }

    [HttpPut("session-templates/{id}")]
    public async Task<IActionResult> UpdateSessionTemplate(Guid id, [FromBody] SessionTemplate update)
    {
        var template = await _db.SessionTemplates.FindAsync(id);
        if (template == null) return NotFound();

        template.DayOfWeek = update.DayOfWeek;
        template.StartTime = update.StartTime;
        template.EndTime = update.EndTime;
        template.Location = update.Location;
        template.MaxCapacity = update.MaxCapacity;
        template.DropInPrice = update.DropInPrice;
        template.IsActive = update.IsActive;
        await _db.SaveChangesAsync();

        return Ok(template);
    }

    [HttpDelete("session-templates/{id}")]
    public async Task<IActionResult> DeleteSessionTemplate(Guid id)
    {
        var template = await _db.SessionTemplates.FindAsync(id);
        if (template == null) return NotFound();
        _db.SessionTemplates.Remove(template);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    #endregion

    #region Email Templates (Saved)

    [HttpGet("email-templates")]
    public async Task<IActionResult> GetEmailTemplates()
    {
        var templates = await _db.SavedEmailTemplates.OrderByDescending(t => t.CreatedOn).ToListAsync();
        return Ok(templates);
    }

    [HttpPost("email-templates")]
    public async Task<IActionResult> CreateEmailTemplate([FromBody] SavedEmailTemplate template)
    {
        _db.SavedEmailTemplates.Add(template);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetEmailTemplates), new { id = template.Id }, template);
    }

    [HttpPut("email-templates/{id}")]
    public async Task<IActionResult> UpdateEmailTemplate(Guid id, [FromBody] SavedEmailTemplate update)
    {
        var template = await _db.SavedEmailTemplates.FindAsync(id);
        if (template == null) return NotFound();

        template.Name = update.Name;
        template.SubjectEn = update.SubjectEn;
        template.SubjectFr = update.SubjectFr;
        template.BodyEn = update.BodyEn;
        template.BodyFr = update.BodyFr;
        template.EmailType = update.EmailType;
        await _db.SaveChangesAsync();

        return Ok(template);
    }

    [HttpDelete("email-templates/{id}")]
    public async Task<IActionResult> DeleteEmailTemplate(Guid id)
    {
        var template = await _db.SavedEmailTemplates.FindAsync(id);
        if (template == null) return NotFound();
        _db.SavedEmailTemplates.Remove(template);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    #endregion

    #region User Stats & Activity

    [HttpGet("users/{userId}/stats")]
    public async Task<IActionResult> GetUserStats(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var attendance = await _db.SessionAttendances
            .Where(a => a.UserId == userId)
            .ToListAsync();
        var payments = await _db.Payments
            .Where(p => p.UserId == userId)
            .ToListAsync();
        var registrations = await _db.SessionRegistrations
            .Where(r => r.UserId == userId)
            .CountAsync();

        var totalAttended = attendance.Count(a => a.IsAttending);
        var totalSessions = attendance.Count;
        var attendanceRate = totalSessions > 0 ? Math.Round((double)totalAttended / totalSessions * 100, 1) : 0;

        // Streak
        var streak = 0;
        var sorted = attendance.Where(a => a.CreatedOn <= DateTime.UtcNow)
            .OrderByDescending(a => a.CreatedOn).ToList();
        foreach (var a in sorted)
        {
            if (a.IsAttending) streak++;
            else break;
        }

        // Engagement tier
        var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);
        var recentAttended = attendance.Count(a => a.CreatedOn >= sixtyDaysAgo && a.IsAttending);
        var recentTotal = await _db.Sessions.CountAsync(s => s.SessionDate >= sixtyDaysAgo && s.SessionDate <= DateTime.UtcNow);
        var recentRate = recentTotal > 0 ? (double)recentAttended / recentTotal * 100 : 0;
        var tier = recentRate >= 80 ? "High" : recentRate >= 50 ? "Medium" : recentRate >= 20 ? "Low" : "Inactive";

        var totalPaid = payments.Where(p => p.Status == Domain.Enums.PaymentStatus.Completed).Sum(p => p.Amount);
        var pendingPayments = payments.Count(p => p.Status == Domain.Enums.PaymentStatus.Pending);
        var lastActive = attendance.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.CreatedOn;

        return Ok(new
        {
            userId,
            name = $"{user.FirstName} {user.LastName}",
            email = user.Email,
            totalSessionsAttended = totalAttended,
            totalSessionsRegistered = registrations,
            attendanceRate,
            currentStreak = streak,
            engagementTier = tier,
            totalPaid,
            pendingPayments,
            lastActive,
            memberSince = user.CreatedOn,
            paymentPlan = user.PaymentPlan.ToString(),
            isAdmin = user.IsAdmin,
            adminNotes = user.AdminNotes,
        });
    }

    [HttpGet("users/{userId}/activity")]
    public async Task<IActionResult> GetUserActivity(Guid userId, [FromQuery] int limit = 50)
    {
        var activities = new List<object>();

        // Attendance records
        var attendance = await _db.SessionAttendances
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedOn)
            .Take(limit)
            .Select(a => new { type = "attendance", date = a.CreatedOn, details = a.IsAttending ? "Attended session" : "Missed session", sessionId = a.SessionId })
            .ToListAsync();
        activities.AddRange(attendance);

        // Payments
        var payments = await _db.Payments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .Take(limit)
            .Select(p => new { type = "payment", date = p.PaymentDate, details = $"Payment ${p.Amount} - {p.Status}", sessionId = (Guid?)null })
            .ToListAsync();
        activities.AddRange(payments);

        // Registrations
        var registrations = await _db.SessionRegistrations
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.RegistrationDate)
            .Take(limit)
            .Select(r => new { type = "registration", date = r.RegistrationDate, details = "Registered for session", sessionId = (Guid?)r.SessionId })
            .ToListAsync();
        activities.AddRange(registrations);

        var sorted = activities
            .OrderByDescending(a => ((dynamic)a).date)
            .Take(limit);

        return Ok(sorted);
    }

    #endregion

    #region User Tags

    [HttpGet("users/{userId}/tags")]
    public async Task<IActionResult> GetUserTags(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();
        // Tags stored as comma-separated in AdminNotes for now (simple approach)
        // In production, use a separate UserTag entity
        var tags = user.AdminNotes?.Split("TAGS:", StringSplitOptions.None);
        var tagList = tags?.Length > 1 ? tags[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList() : new List<string>();
        return Ok(tagList);
    }

    [HttpPost("users/{userId}/tags")]
    public async Task<IActionResult> UpdateUserTags(Guid userId, [FromBody] List<string> tags)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        // Store tags in AdminNotes after "TAGS:" marker
        var notesWithoutTags = user.AdminNotes?.Split("TAGS:")[0].TrimEnd() ?? "";
        var tagStr = string.Join(", ", tags);
        user.AdminNotes = string.IsNullOrEmpty(tagStr) ? notesWithoutTags : $"{notesWithoutTags}\nTAGS:{tagStr}";
        await _db.SaveChangesAsync();

        return Ok(tags);
    }

    #endregion
}

public class UpdateNotesDto
{
    public string? Notes { get; set; }
}

public class ImportUserDto
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public Domain.Enums.PaymentPlan? PaymentPlan { get; set; }
}
