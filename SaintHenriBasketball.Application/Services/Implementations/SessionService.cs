using AutoMapper;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<SessionService> _logger;
    private readonly ICacheService _cacheService;

    // Cache keys
    private const string UpcomingSessionsCacheKey = "UpcomingSessions";
    private const string AvailableSessionsCacheKey = "AvailableSessions";
    private const string SessionKeyPrefix = "Session_";

    public SessionService(
        ISessionRepository sessionRepository,
        ISessionRegistrationRepository registrationRepository,
        IUserRepository userRepository,
        IMapper mapper,
        ILogger<SessionService> logger,
        ICacheService cacheService)
    {
        _sessionRepository = sessionRepository;
        _registrationRepository = registrationRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<SessionDto> CreateSessionAsync(CreateSessionDto createSessionDto)
    {
        if (createSessionDto.SessionDate < DateTime.UtcNow)
        {
            throw new ValidationException("Session date cannot be in the past");
        }

        if (createSessionDto.MaxCapacity <= 0)
        {
            throw new ValidationException("Maximum capacity must be greater than zero");
        }

        if (createSessionDto.DropInPrice < 0)
        {
            throw new ValidationException("Drop-in price cannot be negative");
        }

        var session = new Session(
            createSessionDto.SessionDate,
            createSessionDto.MaxCapacity,
            createSessionDto.DropInPrice,
            createSessionDto.StartTime,
            createSessionDto.EndTime,
            createSessionDto.Location);

        await _sessionRepository.AddAsync(session);
        _logger.LogInformation("Session created successfully. ID: {SessionId}", session.Id);

        // Invalidate affected caches
        await _cacheService.RemoveAsync(UpcomingSessionsCacheKey);
        await _cacheService.RemoveAsync(AvailableSessionsCacheKey);

        return _mapper.Map<SessionDto>(session);
    }

    public async Task<SessionDetailDto> GetSessionAsync(Guid id)
    {
        var session = await _sessionRepository.GetByIdAsync(id);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), id);
        }

        return _mapper.Map<SessionDetailDto>(session);
    }

    public async Task<IReadOnlyList<SessionDto>> GetUpcomingSessionsAsync()
    {
        // Try to get from cache first
        var cachedSessions = await _cacheService.GetAsync<IReadOnlyList<SessionDto>>(UpcomingSessionsCacheKey);

        if (cachedSessions != null)
        {
            _logger.LogInformation("Upcoming sessions retrieved from cache");
            return cachedSessions;
        }

        // If not in cache, get from database
        var sessions = await _sessionRepository.GetUpcomingSessionsAsync();
        var sessionsDto = _mapper.Map<IReadOnlyList<SessionDto>>(sessions);

        await _cacheService.SetAsync(UpcomingSessionsCacheKey, sessionsDto, TimeSpan.FromMinutes(10));

        return sessionsDto;
    }

    public async Task UpdateSessionAsync(Guid id, UpdateSessionDto updateDto)
    {
        var session = await _sessionRepository.GetByIdAsync(id);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), id);
        }

        if (updateDto.SessionDate < DateTime.UtcNow)
        {
            throw new ValidationException("Session date cannot be in the past");
        }

        if (updateDto.MaxCapacity < session.RegisteredPlayersCount)
        {
            throw new ValidationException("New capacity cannot be less than current number of registered players");
        }

        // If changing from Open to other status, validate
        if (session.Status == SessionStatus.Open && updateDto.Status != SessionStatus.Open)
        {
            // Check if there are any registered players before cancelling or completing
            if (session.RegisteredPlayersCount > 0 && updateDto.Status == SessionStatus.Cancelled)
            {
                throw new ValidationException("Cannot cancel session with registered players");
            }
        }

        _mapper.Map(updateDto, session);

        if (session.RegisteredPlayersCount >= session.MaxCapacity)
        {
            session.Status = SessionStatus.Full;
        }

        await _sessionRepository.UpdateAsync(session);
        _logger.LogInformation("Session {SessionId} updated. New status: {Status}", id, session.Status);

        // Invalidate affected caches
        await _cacheService.RemoveAsync(UpcomingSessionsCacheKey);
        await _cacheService.RemoveAsync(AvailableSessionsCacheKey);
        await _cacheService.RemoveAsync($"{SessionKeyPrefix}{id}");
    }
    public async Task CancelSessionAsync(Guid id)
    {
        var session = await _sessionRepository.GetByIdAsync(id);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), id);
        }

        if (session.Status == SessionStatus.Completed)
        {
            throw new ValidationException("Cannot cancel a completed session");
        }

        session.Status = SessionStatus.Cancelled;
        await _sessionRepository.UpdateAsync(session);
        _logger.LogInformation("Session cancelled successfully. ID: {SessionId}", id);

        // Invalidate affected caches
        await _cacheService.RemoveAsync(UpcomingSessionsCacheKey);
        await _cacheService.RemoveAsync(AvailableSessionsCacheKey);
        await _cacheService.RemoveAsync($"{SessionKeyPrefix}{id}");
    }

    public async Task<IReadOnlyList<SessionDto>> GetAvailableSessionsAsync()
    {
        // Try to get from cache first
        var cachedSessions = await _cacheService.GetAsync<IReadOnlyList<SessionDto>>(AvailableSessionsCacheKey);

        if (cachedSessions != null)
        {
            _logger.LogInformation("Available sessions retrieved from cache");
            return cachedSessions;
        }

        // If not in cache, get from database
        var sessions = await _sessionRepository.GetAvailableSessionsAsync();
        var sessionsDto = _mapper.Map<IReadOnlyList<SessionDto>>(sessions);

        // Store in cache with a 10-minute expiration
        await _cacheService.SetAsync(AvailableSessionsCacheKey, sessionsDto, TimeSpan.FromMinutes(10));

        return sessionsDto;
    }

    public async Task<SessionRegistrationResponseDto> RegisterForSessionAsync(Guid sessionId, Guid userId)
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

        if (session.Status != SessionStatus.Open)
        {
            throw new ValidationException("Session is not open for registration");
        }

        if (session.RegisteredPlayersCount >= session.MaxCapacity)
        {
            throw new ValidationException("Session is at full capacity");
        }

        if (await _registrationRepository.ExistsAsync(userId, sessionId))
        {
            throw new ValidationException("User is already registered for this session");
        }

        var registration = new SessionRegistration(userId, sessionId, user.PaymentPlan);
        await _registrationRepository.AddAsync(registration);

        session.RegisteredPlayersCount++;
        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("User {UserId} registered for session {SessionId}", userId, sessionId);

        // Invalidate affected caches
        await _cacheService.RemoveAsync(UpcomingSessionsCacheKey);
        await _cacheService.RemoveAsync(AvailableSessionsCacheKey);
        await _cacheService.RemoveAsync($"{SessionKeyPrefix}{sessionId}");

        return _mapper.Map<SessionRegistrationResponseDto>(registration);
    }

    public async Task UnregisterFromSessionAsync(Guid sessionId, Guid userId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), sessionId);
        }

        if (!await _registrationRepository.ExistsAsync(userId, sessionId))
        {
            throw new NotFoundException("Registration not found");
        }

        await _registrationRepository.DeleteAsync(userId, sessionId);

        session.RegisteredPlayersCount--;
        if (session.Status == SessionStatus.Full && session.RegisteredPlayersCount < session.MaxCapacity)
        {
            session.Status = SessionStatus.Open;
        }

        await _sessionRepository.UpdateAsync(session);
        _logger.LogInformation("User {UserId} unregistered from session {SessionId}", userId, sessionId);

        // Invalidate affected caches
        await _cacheService.RemoveAsync(UpcomingSessionsCacheKey);
        await _cacheService.RemoveAsync(AvailableSessionsCacheKey);
        await _cacheService.RemoveAsync($"{SessionKeyPrefix}{sessionId}");
    }

    public async Task<IReadOnlyList<SessionDto>> GetUserSessionsAsync(Guid userId)
    {
        var registrations = await _registrationRepository.GetByUserIdAsync(userId);
        var sessionIds = registrations.Select(r => r.SessionId).ToList();
        var sessions = await _sessionRepository.GetByIdsAsync(sessionIds);
        return _mapper.Map<IReadOnlyList<SessionDto>>(sessions);
    }

    public async Task<bool> IsUserRegisteredForSessionAsync(Guid sessionId, Guid userId)
    {
        return await _registrationRepository.ExistsAsync(userId, sessionId);
    }

    public async Task<IReadOnlyList<SessionDto>> GetAllSessionsAsync()
    {
        var sessions = await _sessionRepository.GetAllSessionsAsync();
        return _mapper.Map<IReadOnlyList<SessionDto>>(sessions);
    }

    public async Task<SessionRegistrationResponseDto> AddParticipantToSessionAsync(Guid sessionId, Guid userId)
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

        // Admin can add participants even if session is full or closed
        if (await _registrationRepository.ExistsAsync(userId, sessionId))
        {
            throw new ValidationException("User is already registered for this session");
        }

        var registration = new SessionRegistration(userId, sessionId, user.PaymentPlan);
        await _registrationRepository.AddAsync(registration);

        session.RegisteredPlayersCount++;
        
        // Update session status if it was full and now has space
        if (session.Status == SessionStatus.Full && session.RegisteredPlayersCount < session.MaxCapacity)
        {
            session.Status = SessionStatus.Open;
        }
        // If session is now at capacity, mark it as full
        else if (session.RegisteredPlayersCount >= session.MaxCapacity)
        {
            session.Status = SessionStatus.Full;
        }

        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("Admin added user {UserId} to session {SessionId}", userId, sessionId);

        // Invalidate affected caches
        await _cacheService.RemoveAsync(UpcomingSessionsCacheKey);
        await _cacheService.RemoveAsync(AvailableSessionsCacheKey);
        await _cacheService.RemoveAsync($"{SessionKeyPrefix}{sessionId}");

        return _mapper.Map<SessionRegistrationResponseDto>(registration);
    }

    public async Task RemoveParticipantFromSessionAsync(Guid sessionId, Guid userId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), sessionId);
        }

        if (!await _registrationRepository.ExistsAsync(userId, sessionId))
        {
            throw new NotFoundException("Registration not found");
        }

        await _registrationRepository.DeleteAsync(userId, sessionId);

        session.RegisteredPlayersCount--;
        
        // Update session status
        if (session.Status == SessionStatus.Full && session.RegisteredPlayersCount < session.MaxCapacity)
        {
            session.Status = SessionStatus.Open;
        }

        await _sessionRepository.UpdateAsync(session);
        _logger.LogInformation("Admin removed user {UserId} from session {SessionId}", userId, sessionId);

        // Invalidate affected caches
        await _cacheService.RemoveAsync(UpcomingSessionsCacheKey);
        await _cacheService.RemoveAsync(AvailableSessionsCacheKey);
        await _cacheService.RemoveAsync($"{SessionKeyPrefix}{sessionId}");
    }

    public async Task<GenerateSessionsResponseDto> GenerateSessionsForSeasonAsync(GenerateSessionsDto request)
    {
        if (request.EndDate <= request.StartDate)
            throw new ValidationException("End date must be after start date");

        var response = new GenerateSessionsResponseDto();

        // Get all existing sessions to check for duplicates
        var existingSessions = await _sessionRepository.GetAllSessionsAsync();
        var existingDates = new HashSet<DateTime>(
            existingSessions.Select(s => s.SessionDate.Date));

        // Find all Saturdays between start and end date
        var current = request.StartDate.Date;
        // Move to the first Saturday
        while (current.DayOfWeek != DayOfWeek.Saturday)
            current = current.AddDays(1);

        while (current <= request.EndDate.Date)
        {
            if (existingDates.Contains(current))
            {
                response.Skipped++;
                response.SkippedDates.Add(current);
            }
            else
            {
                var session = new Session(
                    current,
                    request.MaxCapacity,
                    request.DropInPrice,
                    request.StartTime,
                    request.EndTime,
                    request.Location);

                await _sessionRepository.AddAsync(session);
                response.Created++;
                response.CreatedDates.Add(current);
            }

            current = current.AddDays(7); // Next Saturday
        }

        // Invalidate caches
        await _cacheService.RemoveAsync(UpcomingSessionsCacheKey);
        await _cacheService.RemoveAsync(AvailableSessionsCacheKey);
        await _cacheService.RemoveAsync("AllSessions");

        response.Message = $"Created {response.Created} sessions. {response.Skipped} skipped (already exist).";
        _logger.LogInformation(
            "Generated {Created} sessions for season {StartDate} to {EndDate}, skipped {Skipped}",
            response.Created, request.StartDate, request.EndDate, response.Skipped);

        return response;
    }
}