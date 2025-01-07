using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Validators;

public class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IAttendanceRepository attendanceRepository,
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IMapper mapper,
        ILogger<AttendanceService> logger)
    {
        _attendanceRepository = attendanceRepository;
        _sessionRepository = sessionRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<AttendanceDto> MarkAttendanceAsync(Guid sessionId, Guid userId, bool isPresent, string? notes)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), sessionId);
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException(nameof(ApplicationUser), userId);
        }

        if (session.SessionDate.Date > DateTime.UtcNow.Date)
        {
            throw new ValidationException("Cannot mark attendance for future sessions");
        }

        var attendance = await _attendanceRepository.GetAttendanceAsync(sessionId, userId);
        if (attendance == null)
        {
            attendance = new SessionAttendance
            {
                SessionId = sessionId,
                UserId = userId,
                IsPresent = isPresent,
                CheckInTime = isPresent ? DateTime.UtcNow : null,
                Notes = notes,
                CreatedOn = DateTime.UtcNow
            };
            await _attendanceRepository.AddAsync(attendance);
        }
        else
        {
            attendance.IsPresent = isPresent;
            attendance.CheckInTime = isPresent ? DateTime.UtcNow : null;
            attendance.Notes = notes;
            await _attendanceRepository.UpdateAsync(attendance);
        }

        _logger.LogInformation(
            "Attendance marked for User {UserId} in Session {SessionId}. Present: {IsPresent}",
            userId, sessionId, isPresent);

        return _mapper.Map<AttendanceDto>(attendance);
    }

    public async Task<SessionAttendanceSummaryDto> GetSessionAttendanceSummaryAsync(Guid sessionId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), sessionId);
        }

        var attendances = await _attendanceRepository.GetSessionAttendancesAsync(sessionId);

        var summary = new SessionAttendanceSummaryDto
        {
            SessionId = sessionId,
            SessionDate = session.SessionDate,
            TotalRegistered = session.RegisteredPlayersCount,
            TotalPresent = attendances.Count(a => a.IsPresent),
            Attendances = _mapper.Map<List<AttendanceDto>>(attendances)
        };

        return summary;
    }

    public async Task<IReadOnlyList<AttendanceDto>> GetUserAttendanceHistoryAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException(nameof(ApplicationUser), userId);
        }

        var attendances = await _attendanceRepository.GetUserAttendancesAsync(userId);
        return _mapper.Map<IReadOnlyList<AttendanceDto>>(attendances);
    }

    public async Task<AttendanceStatsDto> GetAttendanceStatsAsync(DateTime startDate, DateTime endDate)
    {
        var attendances = await _attendanceRepository.GetAttendancesByDateRangeAsync(startDate, endDate);

        return new AttendanceStatsDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalSessions = attendances.Select(a => a.SessionId).Distinct().Count(),
            TotalAttendees = attendances.Where(a => a.IsPresent).Count(),
            AverageAttendanceRate = attendances.Any()
                ? (double)attendances.Count(a => a.IsPresent) / attendances.Count * 100
                : 0,
            SessionStats = attendances
                .GroupBy(a => a.SessionId)
                .Select(g => new SessionStatsDto
                {
                    SessionId = g.Key,
                    SessionDate = g.First().Session.SessionDate,
                    TotalRegistered = g.Count(),
                    TotalPresent = g.Count(a => a.IsPresent),
                    AttendanceRate = (double)g.Count(a => a.IsPresent) / g.Count() * 100
                })
                .ToList()
        };
    }
}