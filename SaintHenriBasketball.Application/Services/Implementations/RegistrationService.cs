using AutoMapper;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class RegistrationService : IRegistrationService
{
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        ISessionRegistrationRepository registrationRepository,
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IMapper mapper,
        ILogger<RegistrationService> logger)
    {
        _registrationRepository = registrationRepository;
        _sessionRepository = sessionRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SessionRegistrationResponseDto> RegisterUserForSessionAsync(Guid userId, Guid sessionId)
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

        // Check if user is already registered
        if (await _registrationRepository.ExistsAsync(userId, sessionId))
        {
            throw new ValidationException("User is already registered for this session");
        }

        if (session.Status != SessionStatus.Open)
        {
            throw new ValidationException("Session is not open for registration");
        }

        if (session.RegisteredPlayersCount >= session.MaxCapacity)
        {
            throw new ValidationException("Session is at maximum capacity");
        }

        var registration = new SessionRegistration(userId, sessionId, user.PaymentPlan);
        await _registrationRepository.AddAsync(registration);

        // Update session's registered players count
        session.RegisteredPlayersCount++;
        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("User {UserId} registered for session {SessionId}", userId, sessionId);

        return _mapper.Map<SessionRegistrationResponseDto>(registration);
    }

    public async Task CancelRegistrationAsync(Guid userId, Guid sessionId)
    {
        var registration = await _registrationRepository.ExistsAsync(userId, sessionId);
        if (!registration)
        {
            throw new NotFoundException("Registration", $"User: {userId}, Session: {sessionId}");
        }

        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), sessionId);
        }

        await _registrationRepository.DeleteAsync(userId, sessionId);

        // Update session's registered players count
        session.RegisteredPlayersCount--;
        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("Registration cancelled for user {UserId} in session {SessionId}",
            userId, sessionId);
    }

    public async Task<IReadOnlyList<SessionRegistrationResponseDto>> GetUserRegistrationsAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException(nameof(ApplicationUser), userId);
        }

        var registrations = await _registrationRepository.GetByUserIdAsync(userId);
        return _mapper.Map<IReadOnlyList<SessionRegistrationResponseDto>>(registrations);
    }
}