using System.Globalization;
using SaintHenriBasketball.Application.DTOs.VolunteerRoles;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class StaffRoleService(IVolunteerRolesRepository repository) : IStaffRoleService
{
    public async Task<StaffRoleChange> SetStaffRoleAsync(Guid userId, string? role)
    {
        if (!StaffAccess.TryParseRole(role, out var next)) throw new ValidationException(StaffAccess.UnknownRoleMessage);
        var target = await repository.GetStaffRoleTargetAsync(userId) ?? throw new NotFoundException("User not found");
        var refusal = StaffAccess.SetRoleRefusal(target.IsAdmin, target.IsDeactivated, next);
        if (refusal != null) throw new ValidationException(refusal);

        if (target.StaffRole != next) await repository.SetStaffRoleAsync(userId, next);
        return new StaffRoleChange(target.Id, $"{target.FirstName} {target.LastName}".Trim(), target.StaffRole, next);
    }
}

public class CourtCaptainService(IVolunteerRolesRepository repository) : ICourtCaptainService
{
    public const int DaysBack = 7;
    public const int DaysAhead = 14;
    public const int MinSearchLength = 2;
    public const int MaxSearchLength = 100;
    public const int MaxPlayers = 20;

    /// Inclusive Montreal calendar dates around today.
    public static (DateTime From, DateTime To) SessionWindow(DateTime utcNow)
    {
        var today = SessionTimeHelper.ToLocal(utcNow).Date;
        return (today.AddDays(-DaysBack), today.AddDays(DaysAhead));
    }

    public async Task<IReadOnlyList<CourtSessionDto>> GetSessionsAsync()
    {
        var (from, to) = SessionWindow(DateTime.UtcNow);
        var rows = await repository.GetCourtSessionsAsync(from, to);
        return rows.Select(r => new CourtSessionDto
        {
            Id = r.Id,
            SessionDate = r.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            StartsAt = DateTime.SpecifyKind(SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(r.SessionDate, r.StartTime)), DateTimeKind.Utc),
            Location = r.Location,
            Status = r.Status.ToString(),
            Registered = r.RegisteredPlayersCount,
            Capacity = r.MaxCapacity,
        }).ToList();
    }

    public async Task<IReadOnlyList<CourtPlayerDto>> SearchPlayersAsync(string? search)
    {
        var term = search?.Trim() ?? string.Empty;
        if (term.Length < MinSearchLength) return Array.Empty<CourtPlayerDto>();
        if (term.Length > MaxSearchLength) term = term[..MaxSearchLength];

        var rows = await repository.SearchActivePlayersAsync(term, MaxPlayers);
        return rows.Select(r => new CourtPlayerDto
        {
            Id = r.Id,
            FirstName = r.FirstName,
            LastName = r.LastName,
            Name = $"{r.FirstName} {r.LastName}".Trim(),
            Email = r.Email,
        }).ToList();
    }
}
