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
