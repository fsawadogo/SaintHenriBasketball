using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.SessionTemplate;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SessionTemplateService : ISessionTemplateService
{
    private const int MaxGenerateRangeDays = 366;

    private readonly ISessionTemplateRepository _templateRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<SessionTemplateService> _logger;

    public SessionTemplateService(
        ISessionTemplateRepository templateRepository,
        ISessionRepository sessionRepository,
        ILogger<SessionTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SessionTemplateDto>> GetAllAsync()
    {
        var templates = await _templateRepository.GetAllAsync();
        return templates.Select(ToDto).ToList();
    }

    public async Task<SessionTemplateDto> CreateAsync(UpsertSessionTemplateDto body)
    {
        ValidateTimeSlot(body);
        var template = new SessionTemplate(
            body.DayOfWeek, body.StartTime, body.EndTime, body.Location,
            body.MaxCapacity, body.DropInPrice)
        { IsActive = body.IsActive };

        await _templateRepository.AddAsync(template);
        return ToDto(template);
    }

    public async Task<SessionTemplateDto> UpdateAsync(Guid id, UpsertSessionTemplateDto body)
    {
        ValidateTimeSlot(body);
        var template = await _templateRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Session template {id} not found");

        template.DayOfWeek = body.DayOfWeek;
        template.StartTime = body.StartTime;
        template.EndTime = body.EndTime;
        template.Location = body.Location;
        template.MaxCapacity = body.MaxCapacity;
        template.DropInPrice = body.DropInPrice;
        template.IsActive = body.IsActive;

        await _templateRepository.UpdateAsync(template);
        return ToDto(template);
    }

    public async Task DeleteAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Session template {id} not found");
        await _templateRepository.DeleteAsync(template.Id);
    }

    public async Task<GenerateSessionsResultDto> GenerateSessionsAsync(Guid templateId, DateTime startDate, DateTime endDate)
    {
        if (endDate.Date < startDate.Date)
            throw new ValidationException("End date must be on or after start date");
        if ((endDate.Date - startDate.Date).TotalDays > MaxGenerateRangeDays)
            throw new ValidationException($"Date range cannot exceed {MaxGenerateRangeDays} days");

        var template = await _templateRepository.GetByIdAsync(templateId)
            ?? throw new NotFoundException($"Session template {templateId} not found");
        if (!template.IsActive)
            throw new ValidationException("Template is inactive");

        var existingSessions = await _sessionRepository.GetAllSessionsAsync();
        var existingKeys = existingSessions
            .Select(s => (s.SessionDate.Date, s.StartTime))
            .ToHashSet();

        var result = new GenerateSessionsResultDto();
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek != template.DayOfWeek) continue;

            if (existingKeys.Contains((date, template.StartTime)))
            {
                result.Skipped++;
                result.SkippedDates.Add(date);
                continue;
            }

            var session = new Session(
                sessionDate: date,
                maxCapacity: template.MaxCapacity,
                dropInPrice: template.DropInPrice,
                startTime: template.StartTime,
                endTime: template.EndTime,
                location: template.Location);

            await _sessionRepository.AddAsync(session);
            result.Created++;
            result.CreatedDates.Add(date);
        }

        _logger.LogInformation(
            "SessionTemplate {TemplateId}: generated {Created} sessions, skipped {Skipped}",
            templateId, result.Created, result.Skipped);

        return result;
    }

    private static void ValidateTimeSlot(UpsertSessionTemplateDto body)
    {
        if (string.IsNullOrWhiteSpace(body.StartTime) || string.IsNullOrWhiteSpace(body.EndTime))
            throw new ValidationException("Start and end times are required");
        if (body.MaxCapacity <= 0)
            throw new ValidationException("Max capacity must be greater than zero");
        if (body.DropInPrice < 0)
            throw new ValidationException("Drop-in price must not be negative");
    }

    private static SessionTemplateDto ToDto(SessionTemplate t) => new()
    {
        Id = t.Id,
        DayOfWeek = t.DayOfWeek,
        StartTime = t.StartTime,
        EndTime = t.EndTime,
        Location = t.Location,
        MaxCapacity = t.MaxCapacity,
        DropInPrice = t.DropInPrice,
        IsActive = t.IsActive,
        CreatedOn = t.CreatedOn,
    };
}
