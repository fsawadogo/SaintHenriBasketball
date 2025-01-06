using AutoMapper;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        ISessionRepository sessionRepository,
        ISessionRegistrationRepository registrationRepository,
        IUserRepository userRepository,
        IMapper mapper,
        ILogger<SessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _registrationRepository = registrationRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _logger = logger;
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
            createSessionDto.DropInPrice);

        await _sessionRepository.AddAsync(session);
        _logger.LogInformation("Session created successfully. ID: {SessionId}", session.Id);

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
        var sessions = await _sessionRepository.GetUpcomingSessionsAsync();
        return _mapper.Map<IReadOnlyList<SessionDto>>(sessions);
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
    }

    public async Task<IReadOnlyList<SessionDto>> GetAvailableSessionsAsync()
    {
        var sessions = await _sessionRepository.GetAvailableSessionsAsync();
        return _mapper.Map<IReadOnlyList<SessionDto>>(sessions);
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
}