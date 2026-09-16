using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.SeasonSchedule;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SeasonScheduleService : ISeasonScheduleService
{
    public const string AuditEntityType = "Season";
    public const string CreatedAction = "Season.CreatedWithSchedule";
    private const string AllSeasonsCacheKey = "AllSeasons";
    private const string CurrentSeasonCacheKey = "CurrentSeason";
    private const string AllSessionsCacheKey = "AllSessions";
    /// Matches the group MemoryCacheService builds from "PublicSchedule:Upcoming:{...}" keys.
    private const string PublicScheduleCachePrefix = "PublicSchedule:Upcoming";

    private readonly ISeasonRepository _seasons;
    private readonly ISeasonScheduleRepository _schedule;
    private readonly IAuditLogService _auditLog;
    private readonly ICacheService _cache;
    private readonly ILogger<SeasonScheduleService> _logger;

    public SeasonScheduleService(
        ISeasonRepository seasons,
        ISeasonScheduleRepository schedule,
        IAuditLogService auditLog,
        ICacheService cache,
        ILogger<SeasonScheduleService> logger)
    {
        _seasons = seasons;
        _schedule = schedule;
        _auditLog = auditLog;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SeasonSchedulePreviewDto> PreviewAsync(SeasonSchedulePreviewRequestDto request)
    {
        var days = SeasonSchedulePlanner.ValidateDays(request.StartDate, request.EndDate, request.Days);
        var planned = SeasonSchedulePlanner.Plan(request.StartDate, request.EndDate, days);
        var existing = await ExistingKeysAsync(request.StartDate, request.EndDate);
        var openSeason = await _seasons.GetCurrentSeasonAsync();

        var sessions = planned.Select(p => new SeasonSchedulePreviewSessionDto
        {
            Date = p.Date,
            DayOfWeek = p.DayOfWeek,
            StartTime = p.StartTime,
            EndTime = p.EndTime,
            MaxCapacity = p.MaxCapacity,
            DropInPrice = p.DropInPrice,
            Location = p.Location,
            AlreadyExists = existing.Contains(SeasonSchedulePlanner.Key(p.Date, p.StartTime)),
        }).ToList();

        return new SeasonSchedulePreviewDto
        {
            Sessions = sessions,
            SessionsToCreate = sessions.Count(s => !s.AlreadyExists),
            SessionsAlreadyExisting = sessions.Count(s => s.AlreadyExists),
            WillBeClosed = openSeason is not null,
            OpenSeasonName = openSeason?.Name,
        };
    }

    public async Task<SeasonScheduleCreateResultDto> CreateAsync(CreateSeasonWithScheduleDto request, Guid? adminId, string adminName)
    {
        SeasonSchedulePlanner.ValidateSeason(request.Name, request.Price, request.Notes);
        var days = SeasonSchedulePlanner.ValidateDays(request.StartDate, request.EndDate, request.Days);
        var planned = SeasonSchedulePlanner.Plan(request.StartDate, request.EndDate, days);

        var existing = await ExistingKeysAsync(request.StartDate, request.EndDate);
        var skipped = (request.Skip ?? new List<SeasonScheduleSkipDto>())
            .Select(s => SeasonSchedulePlanner.Key(s.Date, s.StartTime)).ToHashSet();

        var alreadyExisting = planned.Count(p => existing.Contains(SeasonSchedulePlanner.Key(p.Date, p.StartTime)));
        var remaining = planned.Where(p => !existing.Contains(SeasonSchedulePlanner.Key(p.Date, p.StartTime))).ToList();
        var toCreate = remaining.Where(p => !skipped.Contains(SeasonSchedulePlanner.Key(p.Date, p.StartTime))).ToList();
        var skippedCount = remaining.Count - toCreate.Count;

        // Only one season is open at a time: while another is open, this one is created closed.
        var openSeason = await _seasons.GetCurrentSeasonAsync();
        var season = new Season(Utc(request.StartDate), Utc(request.EndDate), request.Price,
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim())
        {
            Name = request.Name.Trim(),
            Status = openSeason is null ? SeasonStatus.Open : SeasonStatus.Closed,
        };
        var sessions = toCreate
            .Select(p => new Session(p.Date, p.MaxCapacity, p.DropInPrice, p.StartTime, p.EndTime, p.Location))
            .ToList();

        await _schedule.AddSeasonWithSessionsAsync(season, sessions);
        await ClearCachesAsync();
        await TryAuditAsync(season, sessions.Count, skippedCount, alreadyExisting, adminId, adminName);

        return new SeasonScheduleCreateResultDto
        {
            SeasonId = season.Id,
            Name = season.Name,
            Status = season.Status,
            StartDate = season.StartDate,
            EndDate = season.EndDate,
            SessionsCreated = sessions.Count,
            SessionsSkipped = skippedCount,
            SessionsAlreadyExisting = alreadyExisting,
        };
    }

    private static DateTime Utc(DateTime date) => DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    private async Task<HashSet<(DateTime, string)>> ExistingKeysAsync(DateTime from, DateTime to) =>
        (await _schedule.GetActiveSessionsBetweenAsync(from, to))
        .Select(s => SeasonSchedulePlanner.Key(s.SessionDate, s.StartTime))
        .ToHashSet();

    private async Task ClearCachesAsync()
    {
        await _cache.RemoveAsync(AllSeasonsCacheKey);
        await _cache.RemoveAsync(CurrentSeasonCacheKey);
        await _cache.RemoveAsync(SessionCacheKeys.UpcomingSessions);
        await _cache.RemoveAsync(SessionCacheKeys.AvailableSessions);
        await _cache.RemoveAsync(AllSessionsCacheKey);
        await _cache.RemoveByPrefixAsync(PublicScheduleCachePrefix);
    }

    private async Task TryAuditAsync(Season season, int created, int skipped, int alreadyExisting, Guid? adminId, string adminName)
    {
        try
        {
            var details = $"Created {season.Name} ({season.StartDate:yyyy-MM-dd} to {season.EndDate:yyyy-MM-dd}, {season.Status}) with {created} session(s), {skipped} skipped, {alreadyExisting} already existing";
            await _auditLog.LogAsync(CreatedAction, AuditEntityType, season.Id, details.Length > 500 ? details[..500] : details,
                adminId, string.IsNullOrWhiteSpace(adminName) ? "Admin" : adminName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Season {SeasonId} was created but the activity log entry failed", season.Id);
        }
    }
}
