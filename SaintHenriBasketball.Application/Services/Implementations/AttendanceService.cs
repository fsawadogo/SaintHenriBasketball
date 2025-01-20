using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Attendance;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Validators;

public class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IEmailService _emailService;
    private readonly IMapper _mapper;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IAttendanceRepository attendanceRepository,
        ISessionRepository sessionRepository,
        IEmailService emailService,
        IMapper mapper,
        ILogger<AttendanceService> logger)
    {
        _attendanceRepository = attendanceRepository;
        _sessionRepository = sessionRepository;
        _emailService = emailService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<AttendanceResponseDto> MarkAttendanceAsync(
        Guid sessionId,
        Guid userId,
        bool isAttending,
        string? notes)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException($"Session {sessionId} not found");
        }
        
        // Check for existing attendance
        var existingAttendance = await _attendanceRepository.GetAttendanceAsync(sessionId, userId);
        if (existingAttendance != null)
        {
            throw new ValidationException("Attendance already marked for this session");
        }

        var attendance = new SessionAttendance
        {
            SessionId = sessionId,
            UserId = userId,
            IsAttending = isAttending,
            Notes = notes,
            CheckInTime = isAttending ? DateTime.UtcNow : null,
            CreatedOn = DateTime.UtcNow
        };

        // Update session spots if attending
        if (isAttending)
        {
            session.RegisteredPlayersCount++;
            if (session.RegisteredPlayersCount >= session.MaxCapacity)
            {
                session.Status = SessionStatus.Full;
            }
            await _sessionRepository.UpdateAsync(session);
        }

        await _attendanceRepository.AddAsync(attendance);

        // Send confirmation email
        await _emailService.SendAttendanceConfirmationEmailAsync(attendance);

        return _mapper.Map<AttendanceResponseDto>(attendance);
    }

    public async Task<AttendanceResponseDto> UpdateAttendanceAsync(
        Guid sessionId,
        Guid userId,
        bool isAttending,
        string? notes,
        string? updateReason)
    {
        var attendance = await _attendanceRepository.GetAttendanceAsync(sessionId, userId);
        if (attendance == null)
        {
            throw new NotFoundException("Attendance record not found");
        }

        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException($"Session {sessionId} not found");
        }

        // Track previous status for session count update
        var wasAttending = attendance.IsAttending;

        // Update attendance
        attendance.IsAttending = isAttending;
        attendance.Notes = notes;
        attendance.UpdateReason = updateReason;
        attendance.LastUpdated = DateTime.UtcNow;
        attendance.CheckInTime = isAttending ? DateTime.UtcNow : null;

        // Update session spots if status changed
        if (wasAttending != isAttending)
        {
            if (isAttending)
            {
                session.RegisteredPlayersCount++;
                if (session.RegisteredPlayersCount >= session.MaxCapacity)
                {
                    session.Status = SessionStatus.Full;
                }
            }
            else
            {
                session.RegisteredPlayersCount--;
                if (session.Status == SessionStatus.Full && session.RegisteredPlayersCount < session.MaxCapacity)
                {
                    session.Status = SessionStatus.Open;
                }
            }
            await _sessionRepository.UpdateAsync(session);
        }

        await _attendanceRepository.UpdateAsync(attendance);

        // Send update confirmation email
        await _emailService.SendAttendanceUpdateEmailAsync(attendance);

        return _mapper.Map<AttendanceResponseDto>(attendance);
    }

    public async Task<IEnumerable<AttendanceResponseDto>> GetUserAttendanceHistoryAsync(Guid userId)
    {
        var attendances = await _attendanceRepository.GetUserAttendanceHistoryAsync(userId);
        return _mapper.Map<IEnumerable<AttendanceResponseDto>>(attendances);
    }

    public async Task<SessionAttendanceSummaryDto> GetSessionAttendanceSummaryAsync(Guid sessionId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException($"Session {sessionId} not found");
        }

        var attendances = await _attendanceRepository.GetSessionAttendancesAsync(sessionId);

        var sessionAttendances = attendances.ToList();
        return new SessionAttendanceSummaryDto
        {
            SessionId = sessionId,
            SessionDate = session.SessionDate,
            TotalAttendees = sessionAttendances.Count(),
            RegisteredCount = session.RegisteredPlayersCount,
            ConfirmedCount = sessionAttendances.Count(a => a.IsAttending),
            AttendanceRate = session.RegisteredPlayersCount > 0
                ? (decimal)sessionAttendances.Count(a => a.IsAttending) / session.RegisteredPlayersCount * 100
                : 0,
            Attendances = _mapper.Map<List<AttendanceResponseDto>>(attendances)
        };
    }
}