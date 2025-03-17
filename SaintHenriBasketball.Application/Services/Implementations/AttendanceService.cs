using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Attendance;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class AttendanceService : IAttendanceService
{
    private readonly ISessionAttendanceRepository _attendanceRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IEmailService _emailService;
    private readonly IMapper _mapper;
    private readonly ILogger<AttendanceService> _logger;
    private readonly ICacheService _cacheService;

    public AttendanceService(
        ISessionAttendanceRepository attendanceRepository,
        ISessionRepository sessionRepository,
        IEmailService emailService,
        IMapper mapper,
        ILogger<AttendanceService> logger,
        ICacheService cacheService)
    {
        _attendanceRepository = attendanceRepository;
        _sessionRepository = sessionRepository;
        _emailService = emailService;
        _mapper = mapper;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<AttendanceResponseDto> MarkAttendanceAsync(
        Guid sessionId,
        Guid userId,
        bool isAttending,
        string? notes)
    {
        try
        {
            // Check if session exists
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
            {
                throw new NotFoundException($"Session {sessionId} not found");
            }

            // Check for existing attendance
            var hasMarkedAttendance = await _attendanceRepository.HasUserMarkedAttendanceAsync(sessionId, userId);
            if (hasMarkedAttendance)
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
                CreatedOn = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
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

            // Add attendance record
            await _attendanceRepository.AddAsync(attendance);

            // Invalidate cache
            string sessionCacheKey = $"Attendance:Session:{sessionId}";
            string userCacheKey = $"Attendance:User:{userId}";
            await _cacheService.RemoveAsync(sessionCacheKey);
            await _cacheService.RemoveAsync(userCacheKey);

            // Send confirmation email
            await _emailService.SendAttendanceConfirmationEmailAsync(attendance);

            return _mapper.Map<AttendanceResponseDto>(attendance);
        }
        catch (Exception ex) when (ex is not NotFoundException && ex is not ValidationException)
        {
            _logger.LogError(ex, "Error marking attendance for session {SessionId} and user {UserId}",
                sessionId, userId);
            throw;
        }
    }

    public async Task<AttendanceResponseDto> UpdateAttendanceAsync(
        Guid sessionId,
        Guid userId,
        bool isAttending,
        string? notes = null,
        string? updateReason = null)
    {
        try
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
            attendance.Notes = notes ?? attendance.Notes;
            attendance.UpdateReason = updateReason;
            attendance.LastUpdated = DateTime.UtcNow;
            attendance.CheckInTime = isAttending ? (attendance.CheckInTime ?? DateTime.UtcNow) : null;

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

            // Invalidate cache
            string sessionCacheKey = $"Attendance:Session:{sessionId}";
            string userCacheKey = $"Attendance:User:{userId}";
            await _cacheService.RemoveAsync(sessionCacheKey);
            await _cacheService.RemoveAsync(userCacheKey);

            // Send update confirmation email
            await _emailService.SendAttendanceUpdateEmailAsync(attendance, wasAttending, updateReason);

            return _mapper.Map<AttendanceResponseDto>(attendance);
        }
        catch (Exception ex) when (ex is not NotFoundException && ex is not ValidationException)
        {
            _logger.LogError(ex, "Error updating attendance for session {SessionId} and user {UserId}",
                sessionId, userId);
            throw;
        }
    }

    public async Task<IEnumerable<AttendanceResponseDto>> GetUserAttendanceHistoryAsync(Guid userId)
    {
        try
        {
            // Try to get from cache first
            string cacheKey = $"Attendance:User:{userId}";
            var cachedHistory = await _cacheService.GetAsync<IEnumerable<AttendanceResponseDto>>(cacheKey);

            if (cachedHistory != null)
            {
                _logger.LogInformation("Retrieved user attendance history from cache for user {UserId}", userId);
                return cachedHistory;
            }

            // If not in cache, get from repository
            var attendances = await _attendanceRepository.GetUserAttendanceHistoryAsync(userId);
            var result = _mapper.Map<IEnumerable<AttendanceResponseDto>>(attendances);

            // Cache for 10 minutes
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attendance history for user {UserId}", userId);
            throw;
        }
    }

    public async Task<SessionAttendanceSummaryDto> GetSessionAttendanceSummaryAsync(Guid sessionId)
    {
        try
        {
            // Try to get from cache first
            string cacheKey = $"Attendance:Session:{sessionId}:Summary";
            var cachedSummary = await _cacheService.GetAsync<SessionAttendanceSummaryDto>(cacheKey);

            if (cachedSummary != null)
            {
                _logger.LogInformation("Retrieved session attendance summary from cache for session {SessionId}", sessionId);
                return cachedSummary;
            }

            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
            {
                throw new NotFoundException($"Session {sessionId} not found");
            }

            var attendances = await _attendanceRepository.GetSessionAttendancesAsync(sessionId);
            var stats = await _attendanceRepository.GetSessionAttendanceStatsAsync(sessionId);

            var summary = new SessionAttendanceSummaryDto
            {
                SessionId = sessionId,
                SessionDate = session.SessionDate,
                TotalAttendees = stats.Total,
                RegisteredCount = session.RegisteredPlayersCount,
                ConfirmedCount = stats.Present,
                AttendanceRate = session.RegisteredPlayersCount > 0
                    ? (decimal)stats.Present / session.RegisteredPlayersCount * 100
                    : 0,
                Attendances = _mapper.Map<List<AttendanceResponseDto>>(attendances)
            };

            // Cache for 5 minutes
            await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromMinutes(5));

            return summary;
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            _logger.LogError(ex, "Error retrieving attendance summary for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<IEnumerable<AttendanceUserDto>> GetSessionAttendeesAsync(Guid sessionId)
    {
        try
        {
            // Try to get from cache first
            string cacheKey = $"Attendance:Session:{sessionId}:Attendees";
            var cachedAttendees = await _cacheService.GetAsync<IEnumerable<AttendanceUserDto>>(cacheKey);

            if (cachedAttendees != null)
            {
                _logger.LogInformation("Retrieved session attendees from cache for session {SessionId}", sessionId);
                return cachedAttendees;
            }

            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
            {
                throw new NotFoundException($"Session with ID {sessionId} not found");
            }

            var attendances = await _attendanceRepository.GetSessionAttendancesAsync(sessionId);

            var result = attendances.Select(a => new AttendanceUserDto
            {
                UserId = a.UserId,
                FirstName = a.User.FirstName,
                LastName = a.User.LastName,
                Email = a.User.Email,
                IsAttending = a.IsAttending,
                CheckInTime = a.CheckInTime,
                Notes = a.Notes
            })
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToList();

            // Cache for 5 minutes
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

            return result;
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            _logger.LogError(ex, "Error retrieving attendees for session {SessionId}", sessionId);
            throw;
        }
    }
}